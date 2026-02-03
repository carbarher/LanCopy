using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    public enum QueuePriority
    {
        Critical = 0,  // Descargar inmediatamente
        High = 1,      // Antes que normal
        Normal = 2,    // Por defecto
        Low = 3        // Cuando no haya nada más
    }
    
    public class QueuePosition
    {
        public string Username { get; set; }
        public string Filename { get; set; }
        public int Position { get; set; }
        public int TotalInQueue { get; set; }
        public DateTime LastUpdated { get; set; }
        public int EstimatedWaitMinutes { get; set; }
    }
    
    public class QueueManagementSystem
    {
        private Dictionary<string, QueuePosition> queuePositions = new Dictionary<string, QueuePosition>();
        private Dictionary<string, int> autoRetryCount = new Dictionary<string, int>();
        private const int MAX_AUTO_RETRY = 3;
        private Action<string> logAction;
        
        public QueueManagementSystem(Action<string> logger)
        {
            logAction = logger;
        }
        
        public void SetPriority(object task, QueuePriority priority)
        {
            var downloadTask = task as dynamic;
            if (downloadTask != null)
            {
                downloadTask.Priority = (int)priority;
                logAction?.Invoke($"⚡ Prioridad establecida: {downloadTask.FileName} → {priority}");
            }
        }
        
        public void MoveUp(List<object> queue, int index)
        {
            if (index > 0 && index < queue.Count)
            {
                var temp = queue[index];
                queue[index] = queue[index - 1];
                queue[index - 1] = temp;
                logAction?.Invoke($"⬆️ Movido arriba en cola: posición {index} → {index - 1}");
            }
        }
        
        public void MoveDown(List<object> queue, int index)
        {
            if (index >= 0 && index < queue.Count - 1)
            {
                var temp = queue[index];
                queue[index] = queue[index + 1];
                queue[index + 1] = temp;
                logAction?.Invoke($"⬇️ Movido abajo en cola: posición {index} → {index + 1}");
            }
        }
        
        public void MoveToPosition(List<object> queue, int fromIndex, int toIndex)
        {
            if (fromIndex >= 0 && fromIndex < queue.Count && toIndex >= 0 && toIndex < queue.Count)
            {
                var item = queue[fromIndex];
                queue.RemoveAt(fromIndex);
                queue.Insert(toIndex, item);
                logAction?.Invoke($"↔️ Movido en cola: posición {fromIndex} → {toIndex}");
            }
        }
        
        public void MoveToTop(List<object> queue, int index)
        {
            if (index > 0 && index < queue.Count)
            {
                var item = queue[index];
                queue.RemoveAt(index);
                queue.Insert(0, item);
                logAction?.Invoke($"⏫ Movido al inicio de la cola");
            }
        }
        
        public void MoveToBottom(List<object> queue, int index)
        {
            if (index >= 0 && index < queue.Count - 1)
            {
                var item = queue[index];
                queue.RemoveAt(index);
                queue.Add(item);
                logAction?.Invoke($"⏬ Movido al final de la cola");
            }
        }
        
        public List<object> SortByPriority(List<object> queue)
        {
            return queue.OrderBy(item =>
            {
                var task = item as dynamic;
                return task?.Priority ?? (int)QueuePriority.Normal;
            }).ToList();
        }
        
        public void UpdateQueuePosition(string username, string filename, int position, int total)
        {
            string key = $"{username}|{filename}";
            
            queuePositions[key] = new QueuePosition
            {
                Username = username,
                Filename = filename,
                Position = position,
                TotalInQueue = total,
                LastUpdated = DateTime.Now,
                EstimatedWaitMinutes = EstimateWaitTime(position, total)
            };
            
            logAction?.Invoke($"📊 Posición en cola actualizada: {filename} → {position}/{total}");
        }
        
        public QueuePosition GetQueuePosition(string username, string filename)
        {
            string key = $"{username}|{filename}";
            return queuePositions.TryGetValue(key, out var pos) ? pos : null;
        }
        
        public bool ShouldAutoRetry(string taskId, string errorMessage)
        {
            if (!autoRetryCount.ContainsKey(taskId))
            {
                autoRetryCount[taskId] = 0;
            }
            
            bool shouldRetry = autoRetryCount[taskId] < MAX_AUTO_RETRY &&
                              (errorMessage.Contains("Can't connect") ||
                               errorMessage.Contains("Connection timeout") ||
                               errorMessage.Contains("Remote closed"));
            
            if (shouldRetry)
            {
                autoRetryCount[taskId]++;
                logAction?.Invoke($"🔄 Auto-retry {autoRetryCount[taskId]}/{MAX_AUTO_RETRY}: {taskId}");
            }
            else if (autoRetryCount[taskId] >= MAX_AUTO_RETRY)
            {
                logAction?.Invoke($"⛔ Máximo de reintentos alcanzado: {taskId}");
            }
            
            return shouldRetry;
        }
        
        public void ResetAutoRetryCount(string taskId)
        {
            autoRetryCount.Remove(taskId);
        }
        
        public int GetAutoRetryCount(string taskId)
        {
            return autoRetryCount.TryGetValue(taskId, out int count) ? count : 0;
        }
        
        private int EstimateWaitTime(int position, int total)
        {
            if (position <= 0 || total <= 0) return 0;
            
            // Estimar 3 minutos por archivo en promedio
            int avgMinutesPerFile = 3;
            return position * avgMinutesPerFile;
        }
        
        public Panel CreateQueueManagementPanel(ListView lvQueue, Func<List<object>> getQueue, Action saveQueue)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(5)
            };
            
            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true
            };
            
            // Botones de prioridad
            var btnCritical = CreatePriorityButton("🔴 Critical", QueuePriority.Critical, Color.FromArgb(150, 0, 0));
            var btnHigh = CreatePriorityButton("🟠 High", QueuePriority.High, Color.FromArgb(150, 75, 0));
            var btnNormal = CreatePriorityButton("🟢 Normal", QueuePriority.Normal, Color.FromArgb(0, 100, 0));
            var btnLow = CreatePriorityButton("🔵 Low", QueuePriority.Low, Color.FromArgb(0, 0, 150));
            
            btnCritical.Click += (s, e) => SetPriorityForSelected(lvQueue, QueuePriority.Critical, saveQueue);
            btnHigh.Click += (s, e) => SetPriorityForSelected(lvQueue, QueuePriority.High, saveQueue);
            btnNormal.Click += (s, e) => SetPriorityForSelected(lvQueue, QueuePriority.Normal, saveQueue);
            btnLow.Click += (s, e) => SetPriorityForSelected(lvQueue, QueuePriority.Low, saveQueue);
            
            // Separador
            var separator1 = new Label { Text = "|", ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(5, 8, 5, 0) };
            
            // Botones de movimiento
            var btnMoveTop = CreateMoveButton("⏫ Top");
            var btnMoveUp = CreateMoveButton("⬆️ Up");
            var btnMoveDown = CreateMoveButton("⬇️ Down");
            var btnMoveBottom = CreateMoveButton("⏬ Bottom");
            
            btnMoveTop.Click += (s, e) => MoveSelectedInQueue(lvQueue, getQueue, saveQueue, "top");
            btnMoveUp.Click += (s, e) => MoveSelectedInQueue(lvQueue, getQueue, saveQueue, "up");
            btnMoveDown.Click += (s, e) => MoveSelectedInQueue(lvQueue, getQueue, saveQueue, "down");
            btnMoveBottom.Click += (s, e) => MoveSelectedInQueue(lvQueue, getQueue, saveQueue, "bottom");
            
            // Separador
            var separator2 = new Label { Text = "|", ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(5, 8, 5, 0) };
            
            // Botón de ordenar
            var btnSort = CreateActionButton("🔀 Sort by Priority");
            btnSort.Click += (s, e) =>
            {
                var queue = getQueue();
                var sorted = SortByPriority(queue);
                queue.Clear();
                queue.AddRange(sorted);
                saveQueue();
                RefreshListView(lvQueue, queue);
                logAction?.Invoke("🔀 Cola ordenada por prioridad");
            };
            
            flowPanel.Controls.AddRange(new Control[] 
            { 
                btnCritical, btnHigh, btnNormal, btnLow,
                separator1,
                btnMoveTop, btnMoveUp, btnMoveDown, btnMoveBottom,
                separator2,
                btnSort
            });
            
            panel.Controls.Add(flowPanel);
            
            return panel;
        }
        
        private Button CreatePriorityButton(string text, QueuePriority priority, Color color)
        {
            return new Button
            {
                Text = text,
                Width = 90,
                Height = 35,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(2)
            };
        }
        
        private Button CreateMoveButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 80,
                Height = 35,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand,
                Margin = new Padding(2)
            };
        }
        
        private Button CreateActionButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 140,
                Height = 35,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(2)
            };
        }
        
        private void SetPriorityForSelected(ListView lv, QueuePriority priority, Action saveQueue)
        {
            if (lv.SelectedItems.Count == 0) return;
            
            foreach (ListViewItem item in lv.SelectedItems)
            {
                if (item.Tag != null)
                {
                    SetPriority(item.Tag, priority);
                    
                    // Actualizar color del item
                    item.BackColor = GetPriorityColor(priority);
                }
            }
            
            saveQueue();
        }
        
        private void MoveSelectedInQueue(ListView lv, Func<List<object>> getQueue, Action saveQueue, string direction)
        {
            if (lv.SelectedItems.Count == 0) return;
            
            var queue = getQueue();
            int index = lv.SelectedIndices[0];
            
            switch (direction)
            {
                case "top":
                    MoveToTop(queue, index);
                    break;
                case "up":
                    MoveUp(queue, index);
                    break;
                case "down":
                    MoveDown(queue, index);
                    break;
                case "bottom":
                    MoveToBottom(queue, index);
                    break;
            }
            
            saveQueue();
            RefreshListView(lv, queue);
        }
        
        private void RefreshListView(ListView lv, List<object> queue)
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            
            foreach (var item in queue)
            {
                var task = item as dynamic;
                if (task != null)
                {
                    var lvItem = new ListViewItem(task.FileName?.ToString() ?? "");
                    lvItem.Tag = item;
                    
                    var priority = (QueuePriority)(task.Priority ?? (int)QueuePriority.Normal);
                    lvItem.BackColor = GetPriorityColor(priority);
                    
                    lv.Items.Add(lvItem);
                }
            }
            
            lv.EndUpdate();
        }
        
        private Color GetPriorityColor(QueuePriority priority)
        {
            switch (priority)
            {
                case QueuePriority.Critical:
                    return Color.FromArgb(80, 0, 0);
                case QueuePriority.High:
                    return Color.FromArgb(80, 40, 0);
                case QueuePriority.Normal:
                    return Color.FromArgb(35, 35, 35);
                case QueuePriority.Low:
                    return Color.FromArgb(0, 0, 80);
                default:
                    return Color.FromArgb(35, 35, 35);
            }
        }
    }
}
