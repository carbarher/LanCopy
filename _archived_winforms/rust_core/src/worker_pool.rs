// Worker Pool thread-safe para procesamiento paralelo
// Arquitectura: C# envía tareas → Workers procesan → C# recibe resultados
// Sin race conditions en FFI

use std::sync::{Arc, Mutex};
use std::sync::mpsc::{channel, Sender, Receiver};
use std::thread;
use rayon::prelude::*;

/// Tipos de tareas que el worker pool puede procesar
#[derive(Clone)]
pub enum Task {
    Sort(Vec<String>),
    Distinct(Vec<String>),
    Filter { items: Vec<String>, pattern: String, case_sensitive: bool },
}

/// Resultado de una tarea procesada
pub enum TaskResult {
    Sort(Vec<String>),
    Distinct(Vec<String>),
    Filter(Vec<String>),
}

/// Worker Pool global thread-safe
pub struct WorkerPool {
    task_sender: Sender<(usize, Task)>,
    result_receiver: Arc<Mutex<Receiver<(usize, TaskResult)>>>,
}

impl WorkerPool {
    /// Crea un nuevo worker pool con N workers
    pub fn new(num_workers: usize) -> Self {
        let (task_tx, task_rx) = channel::<(usize, Task)>();
        let (result_tx, result_rx) = channel::<(usize, TaskResult)>();
        
        let task_rx = Arc::new(Mutex::new(task_rx));
        
        // Crear workers
        for _ in 0..num_workers {
            let task_rx = Arc::clone(&task_rx);
            let result_tx = result_tx.clone();
            
            thread::spawn(move || {
                loop {
                    let task = {
                        let rx = task_rx.lock().unwrap();
                        rx.recv()
                    };
                    
                    match task {
                        Ok((task_id, task)) => {
                            let result = Self::process_task(task);
                            let _ = result_tx.send((task_id, result));
                        }
                        Err(_) => break, // Canal cerrado
                    }
                }
            });
        }
        
        WorkerPool {
            task_sender: task_tx,
            result_receiver: Arc::new(Mutex::new(result_rx)),
        }
    }
    
    /// Procesa una tarea
    fn process_task(task: Task) -> TaskResult {
        match task {
            Task::Sort(mut items) => {
                items.par_sort_by(|a, b| a.to_lowercase().cmp(&b.to_lowercase()));
                TaskResult::Sort(items)
            }
            Task::Distinct(items) => {
                use std::collections::HashSet;
                let seen = Mutex::new(HashSet::new());
                let distinct: Vec<String> = items.into_par_iter()
                    .filter(|s| {
                        let key = s.to_lowercase();
                        seen.lock().unwrap().insert(key)
                    })
                    .collect();
                TaskResult::Distinct(distinct)
            }
            Task::Filter { items, pattern, case_sensitive } => {
                let filtered: Vec<String> = if case_sensitive {
                    items.par_iter()
                        .filter(|s| s.contains(&pattern))
                        .cloned()
                        .collect()
                } else {
                    let pattern_lower = pattern.to_lowercase();
                    items.par_iter()
                        .filter(|s| s.to_lowercase().contains(&pattern_lower))
                        .cloned()
                        .collect()
                };
                TaskResult::Filter(filtered)
            }
        }
    }
    
    /// Envía una tarea al pool
    pub fn submit(&self, task_id: usize, task: Task) -> Result<(), String> {
        self.task_sender.send((task_id, task))
            .map_err(|e| format!("Error enviando tarea: {}", e))
    }
    
    /// Recibe el resultado de una tarea (bloqueante)
    pub fn receive(&self) -> Result<(usize, TaskResult), String> {
        let rx = self.result_receiver.lock().unwrap();
        rx.recv().map_err(|e| format!("Error recibiendo resultado: {}", e))
    }
    
    /// Intenta recibir un resultado sin bloquear
    pub fn try_receive(&self) -> Option<(usize, TaskResult)> {
        let rx = self.result_receiver.lock().unwrap();
        rx.try_recv().ok()
    }
}

// Worker Pool global singleton
use once_cell::sync::Lazy;

static WORKER_POOL: Lazy<WorkerPool> = Lazy::new(|| {
    let num_cpus = num_cpus::get();
    WorkerPool::new(num_cpus.max(4)) // Mínimo 4 workers
});

/// Obtiene referencia al worker pool global
pub fn get_pool() -> &'static WorkerPool {
    &WORKER_POOL
}
