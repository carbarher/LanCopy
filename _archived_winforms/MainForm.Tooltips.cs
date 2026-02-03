using System;
using System.Windows.Forms;

namespace SlskDown
{
    partial class MainForm
    {
        private ToolTip mainToolTip;
        
        /// <summary>
        /// Inicializa todos los tooltips descriptivos de la aplicación
        /// </summary>
        private void InitializeTooltips()
        {
            mainToolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 500,
                ReshowDelay = 200,
                ShowAlways = true,
                IsBalloon = false,
                UseAnimation = true,
                UseFading = true
            };
            
            // === PESTAÑA BÚSQUEDA ===
            // Botones principales
            if (btnConnect != null)
                mainToolTip.SetToolTip(btnConnect, "Establece conexión con la red Soulseek usando tus credenciales configuradas");
            
            if (btnSearch != null)
                mainToolTip.SetToolTip(btnSearch, "Ejecuta una búsqueda en la red P2P con el término ingresado. Busca en todos los usuarios conectados");
            
            if (btnStopSearch != null)
                mainToolTip.SetToolTip(btnStopSearch, "Cancela la búsqueda actual y detiene la recopilación de resultados");
            
            if (btnClearResults != null)
                mainToolTip.SetToolTip(btnClearResults, "Elimina todos los resultados de búsqueda de la lista actual");
            
            if (btnExportCSV != null)
                mainToolTip.SetToolTip(btnExportCSV, "Exporta los resultados de búsqueda actuales a un archivo CSV para análisis externo");
            
            if (btnOpenDownloadFolder != null)
                mainToolTip.SetToolTip(btnOpenDownloadFolder, "Abre el explorador de Windows en la carpeta donde se guardan las descargas completadas");
            
            if (btnToggleFilters != null)
                mainToolTip.SetToolTip(btnToggleFilters, "Muestra u oculta el panel de filtros avanzados para refinar los resultados de búsqueda");
            
            // Campos de entrada - Búsqueda
            if (cmbSearch != null)
                mainToolTip.SetToolTip(cmbSearch, "Ingresa el término de búsqueda (autor, título, palabra clave). Usa comillas para búsquedas exactas");
            
            if (txtFilterResults != null)
                mainToolTip.SetToolTip(txtFilterResults, "Filtra los resultados mostrados en tiempo real escribiendo texto para buscar en nombres de archivo");
            
            // Checkboxes - Búsqueda
            if (chkSpanishOnly != null)
                mainToolTip.SetToolTip(chkSpanishOnly, "Filtra resultados para mostrar solo archivos detectados como contenido en español");
            
            if (chkQualityFilter != null)
                mainToolTip.SetToolTip(chkQualityFilter, "Activa el filtro de calidad mínima para archivos de audio (bitrate)");
            
            if (chkOnlyFreeSlots != null)
                mainToolTip.SetToolTip(chkOnlyFreeSlots, "Muestra solo resultados de usuarios que tienen slots de descarga disponibles en este momento");
            
            // Controles numéricos - Búsqueda
            if (numMinQuality != null)
                mainToolTip.SetToolTip(numMinQuality, "Calidad mínima requerida para archivos de audio (kbps). Valores típicos: 128, 192, 256, 320");
            
            if (numMinSize != null)
                mainToolTip.SetToolTip(numMinSize, "Tamaño mínimo de archivo en KB. Útil para filtrar archivos muy pequeños o de baja calidad");
            
            if (numMaxSize != null)
                mainToolTip.SetToolTip(numMaxSize, "Tamaño máximo de archivo en KB. Útil para excluir archivos muy grandes");
            
            if (numMinBitrate != null)
                mainToolTip.SetToolTip(numMinBitrate, "Bitrate mínimo para archivos de audio en kbps (32, 64, 128, 192, 256, 320)");
            
            // ComboBoxes - Búsqueda
            if (cmbExtension != null)
                mainToolTip.SetToolTip(cmbExtension, "Filtra por categoría de archivo: Documentos (epub, pdf, mobi), Comics (cbr, cbz), Videos, Música, etc.");
            
            if (cmbExtensionFilter != null)
                mainToolTip.SetToolTip(cmbExtensionFilter, "Filtra por extensión específica de archivo (.epub, .pdf, .mobi, .mp3, .flac, etc.)");
            
            if (cmbSortBy != null)
                mainToolTip.SetToolTip(cmbSortBy, "Ordena los resultados por: Relevancia, Tamaño, Nombre, Usuario o Velocidad de descarga estimada");
            
            // === PESTAÑA CHAT AI ===
            if (btnChatSend != null)
                mainToolTip.SetToolTip(btnChatSend, "Envía tu mensaje al asistente de IA local (Ollama) para obtener ayuda o recomendaciones");
            
            if (btnChatClear != null)
                mainToolTip.SetToolTip(btnChatClear, "Limpia todo el historial de conversación con el asistente de IA");
            
            // === PESTAÑA AUTO ===
            if (btnStartAuto != null)
                mainToolTip.SetToolTip(btnStartAuto, "Inicia el proceso de búsqueda automática para todos los autores seleccionados de la lista");
            
            if (btnStopAuto != null)
                mainToolTip.SetToolTip(btnStopAuto, "Detiene el proceso de búsqueda automática en curso y guarda el progreso actual");
            
            if (btnOpenAutoResults != null)
                mainToolTip.SetToolTip(btnOpenAutoResults, "Abre el archivo CSV con los resultados de las búsquedas automáticas ejecutadas");
            
            if (btnPurge != null)
                mainToolTip.SetToolTip(btnPurge, "Elimina autores sin resultados de la lista para optimizar futuras búsquedas");
            
            // NOTA: Los controles dinámicos de cada pestaña (como chkOnlyNew, etc.)
            // se configuran cuando se crean usando el método AddTooltip() en los métodos Create*Tab
        }
        
        /// <summary>
        /// Helper para agregar tooltips a controles dinámicos
        /// </summary>
        public void AddTooltip(Control control, string text)
        {
            if (control != null && mainToolTip != null)
            {
                mainToolTip.SetToolTip(control, text);
            }
        }
        
        /// <summary>
        /// Configura tooltips específicos para la pestaña de Descargas
        /// Llamar después de crear los botones en CreateDownloadsTab
        /// </summary>
        public void SetupDownloadsTabTooltips(
            Control btnClearAll,
            Control btnRetryFailed,
            Control btnPauseAll,
            Control btnResumeAll,
            Control btnCancelAll,
            Control btnExportQueue,
            Control btnOpenFolder)
        {
            AddTooltip(btnClearAll, "Elimina todas las entradas de la lista de descargas, incluyendo cola, historial y estadísticas de proveedores");
            AddTooltip(btnRetryFailed, "Reintenta automáticamente todas las descargas que han fallado, reseteando los contadores de error");
            AddTooltip(btnPauseAll, "Pausa todas las descargas activas y en cola, manteniendo el progreso actual para reanudar después");
            AddTooltip(btnResumeAll, "Reanuda todas las descargas pausadas y continúa procesando la cola desde donde se detuvo");
            AddTooltip(btnCancelAll, "Cancela permanentemente todas las descargas activas y las elimina de la cola sin posibilidad de reanudar");
            AddTooltip(btnExportQueue, "Exporta la cola de descargas actual a un archivo CSV para respaldo o análisis externo");
            AddTooltip(btnOpenFolder, "Abre el explorador de Windows en la carpeta de descargas configurada para acceder a los archivos");
        }
        
        /// <summary>
        /// Configura tooltips para la pestaña de Autores
        /// </summary>
        public void SetupAuthorsTabTooltips(
            Control btnLoadAuthors,
            Control btnSaveAuthors,
            Control btnClearAuthors,
            Control btnSearchSelected,
            Control btnRemoveSelected,
            Control btnSortAZ,
            Control btnSortZA)
        {
            AddTooltip(btnLoadAuthors, "Carga una lista de autores desde un archivo de texto (un autor por línea) para búsquedas masivas");
            AddTooltip(btnSaveAuthors, "Guarda la lista actual de autores en un archivo de texto para uso futuro o respaldo");
            AddTooltip(btnClearAuthors, "Elimina todos los autores de la lista actual sin afectar los archivos guardados");
            AddTooltip(btnSearchSelected, "Ejecuta búsquedas simultáneas para todos los autores seleccionados en la lista");
            AddTooltip(btnRemoveSelected, "Elimina de la lista solo los autores que están actualmente seleccionados");
            AddTooltip(btnSortAZ, "Ordena la lista de autores alfabéticamente de A a Z para facilitar la navegación");
            AddTooltip(btnSortZA, "Ordena la lista de autores alfabéticamente de Z a A (orden inverso)");
        }
        
        /// <summary>
        /// Configura tooltips para la pestaña de Archivos
        /// </summary>
        public void SetupFilesTabTooltips(
            Control btnRefreshFiles,
            Control btnDownloadSelected,
            Control btnDeleteSelected,
            Control btnOpenFileLocation,
            Control btnFilterDuplicates,
            Control btnExportFileList)
        {
            AddTooltip(btnRefreshFiles, "Actualiza la lista de archivos escaneando nuevamente la carpeta de descargas para detectar cambios");
            AddTooltip(btnDownloadSelected, "Agrega los archivos seleccionados a la cola de descargas para obtenerlos de la red P2P");
            AddTooltip(btnDeleteSelected, "Elimina permanentemente del disco los archivos seleccionados (requiere confirmación)");
            AddTooltip(btnOpenFileLocation, "Abre el explorador de Windows en la ubicación del archivo seleccionado");
            AddTooltip(btnFilterDuplicates, "Muestra solo los archivos duplicados detectados por nombre, tamaño o hash para facilitar limpieza");
            AddTooltip(btnExportFileList, "Exporta la lista completa de archivos a CSV con metadatos (tamaño, fecha, ruta, etc.)");
        }
        
        /// <summary>
        /// Configura tooltips para la pestaña de Wishlist
        /// </summary>
        public void SetupWishlistTabTooltips(
            Control btnAddWish,
            Control btnRemoveWish,
            Control btnSearchWishlist,
            Control btnClearWishlist,
            Control btnEnableAutoSearch,
            Control btnConfigureInterval)
        {
            AddTooltip(btnAddWish, "Agrega un nuevo término de búsqueda a la lista de deseos para monitoreo automático");
            AddTooltip(btnRemoveWish, "Elimina los términos seleccionados de la lista de deseos");
            AddTooltip(btnSearchWishlist, "Ejecuta búsquedas inmediatas para todos los términos de la wishlist sin esperar el intervalo automático");
            AddTooltip(btnClearWishlist, "Elimina todos los términos de la lista de deseos (requiere confirmación)");
            AddTooltip(btnEnableAutoSearch, "Activa/desactiva la búsqueda automática periódica de los términos en la wishlist");
            AddTooltip(btnConfigureInterval, "Configura cada cuántos minutos se ejecutan las búsquedas automáticas de la wishlist");
        }
        
        /// <summary>
        /// Configura tooltips para la pestaña de Calibre
        /// </summary>
        public void SetupCalibreTabTooltips(
            Control btnConnectCalibre,
            Control btnSyncLibrary,
            Control btnImportToQueue,
            Control btnExportMetadata,
            Control btnConfigurePath)
        {
            AddTooltip(btnConnectCalibre, "Establece conexión con la biblioteca de Calibre para sincronizar metadatos y libros");
            AddTooltip(btnSyncLibrary, "Sincroniza la biblioteca de Calibre con los archivos descargados, actualizando metadatos automáticamente");
            AddTooltip(btnImportToQueue, "Importa libros faltantes de la biblioteca Calibre a la cola de descargas para buscarlos en la red");
            AddTooltip(btnExportMetadata, "Exporta los metadatos de Calibre (autor, título, serie, etc.) a un archivo para análisis");
            AddTooltip(btnConfigurePath, "Configura la ruta de la biblioteca de Calibre en tu sistema para la integración");
        }
        
        /// <summary>
        /// Configura tooltips para la pestaña Auto
        /// </summary>
        public void SetupAutoTabTooltips(
            Control btnStartAuto,
            Control btnStopAuto,
            Control btnLoadAuthorList,
            Control btnSaveProgress,
            Control btnClearResults,
            Control btnExportResults,
            Control btnConfigureFilters)
        {
            AddTooltip(btnStartAuto, "Inicia el proceso de búsqueda automática masiva para todos los autores de la lista cargada");
            AddTooltip(btnStopAuto, "Detiene el proceso de búsqueda automática en curso y cancela las búsquedas pendientes");
            AddTooltip(btnLoadAuthorList, "Carga una lista de autores desde un archivo de texto para procesamiento automático");
            AddTooltip(btnSaveProgress, "Guarda el progreso actual de las búsquedas automáticas para continuar más tarde");
            AddTooltip(btnClearResults, "Limpia todos los resultados de búsquedas automáticas acumulados");
            AddTooltip(btnExportResults, "Exporta los resultados de búsquedas automáticas a un archivo CSV");
            AddTooltip(btnConfigureFilters, "Configura filtros avanzados para las búsquedas automáticas (tamaño, formato, etc.)");
        }
    }
}
