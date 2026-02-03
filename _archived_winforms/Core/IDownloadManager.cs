using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Interfaz para el gestor de descargas
    /// </summary>
    public interface IDownloadManager
    {
        /// <summary>
        /// Inicia el gestor de descargas
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Detiene el gestor de descargas
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Agrega una tarea a la cola de descargas
        /// </summary>
        void AddToQueue(DownloadTask task);

        /// <summary>
        /// Cancela una descarga
        /// </summary>
        void CancelDownload(DownloadTask task);

        /// <summary>
        /// Pausa una descarga
        /// </summary>
        void PauseDownload(DownloadTask task);

        /// <summary>
        /// Reanuda una descarga pausada
        /// </summary>
        void ResumeDownload(DownloadTask task);

        /// <summary>
        /// Obtiene todas las tareas en la cola
        /// </summary>
        IReadOnlyList<DownloadTask> GetQueuedTasks();

        /// <summary>
        /// Obtiene las tareas activas (descargando)
        /// </summary>
        IReadOnlyList<DownloadTask> GetActiveTasks();

        /// <summary>
        /// Obtiene las tareas completadas
        /// </summary>
        IReadOnlyList<DownloadTask> GetCompletedTasks();

        /// <summary>
        /// Obtiene las tareas fallidas
        /// </summary>
        IReadOnlyList<DownloadTask> GetFailedTasks();

        /// <summary>
        /// Limpia tareas completadas o canceladas
        /// </summary>
        void ClearCompletedTasks();

        /// <summary>
        /// Guarda la cola de descargas
        /// </summary>
        Task SaveQueueAsync();

        /// <summary>
        /// Carga la cola de descargas
        /// </summary>
        Task LoadQueueAsync();

        /// <summary>
        /// Indica si el gestor está en ejecución
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Número máximo de descargas simultáneas
        /// </summary>
        int MaxSimultaneousDownloads { get; set; }

        /// <summary>
        /// Estrategia de priorización de cola
        /// </summary>
        QueuePrioritizationStrategy QueueStrategy { get; set; }
    }

    /// <summary>
    /// Callbacks para comunicación con la UI
    /// </summary>
    public class DownloadManagerCallbacks
    {
        /// <summary>
        /// Callback cuando se actualiza el progreso de una descarga
        /// </summary>
        public Action<DownloadTask, string> OnProgressUpdated { get; set; }

        /// <summary>
        /// Callback cuando una descarga se completa
        /// </summary>
        public Action<DownloadTask> OnDownloadCompleted { get; set; }

        /// <summary>
        /// Callback cuando una descarga falla
        /// </summary>
        public Action<DownloadTask, string> OnDownloadFailed { get; set; }

        /// <summary>
        /// Callback para logging
        /// </summary>
        public Action<string> OnLog { get; set; }

        /// <summary>
        /// Callback para mostrar notificaciones
        /// </summary>
        public Action<string, string> OnShowNotification { get; set; }

        /// <summary>
        /// Callback para actualizar estadísticas
        /// </summary>
        public Action<ProviderStats> OnStatsUpdated { get; set; }
    }
}
