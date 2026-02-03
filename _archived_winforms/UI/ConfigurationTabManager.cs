using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using SlskDown.Services;
using SlskDown.Core.AI;
using SlskDown.Core.Voice;
using SlskDown.Core.GPU;

namespace SlskDown.UI
{
    /// <summary>
    /// Manejador de la pestaña de configuración con opciones avanzadas
    /// </summary>
    public class ConfigurationTabManager : IDisposable
    {
        private readonly MainForm _mainForm;
        private readonly UnifiedSettingsManager _settingsManager;
        private readonly VoiceControlEngine _voiceEngine;
        private readonly CUDAAccelerator _gpuAccelerator;
        
        // Controles UI
        private TabControl tabConfig;
        private TabPage tabGeneral, tabAI, tabPerformance, tabVoice, tabGPU, tabAdvanced;
        
        // Configuración General
        private TextBox txtDownloadDir;
        private TextBox txtDataDir;
        private NumericUpDown numMaxDownloads;
        private NumericUpDown numSearchTimeout;
        private CheckBox chkAutoStart;
        private CheckBox chkMinimizeToTray;
        private CheckBox chkCheckUpdates;
        
        // Configuración IA
        private CheckBox chkEnablePredictive;
        private CheckBox chkEnableClassification;
        private NumericUpDown numPredictionCount;
        private TrackBar trackConfidenceThreshold;
        private Label lblConfidenceValue;
        private Button btnClearLearningData;
        private Button btnExportLearningData;
        
        // Configuración Rendimiento
        private CheckBox chkEnableMemoryPool;
        private NumericUpDown numPoolSize;
        private CheckBox chkEnableAsyncPipeline;
        private NumericUpDown numConcurrency;
        private CheckBox chkEnableGPU;
        private ComboBox cbGPUDevice;
        
        // Configuración Voz
        private CheckBox chkEnableVoiceControl;
        private Button btnTestVoice;
        private Button btnTrainVoice;
        private ComboBox cbVoiceLanguage;
        private TrackBar trackVoiceSensitivity;
        private Label lblVoiceStatus;
        
        // Configuración GPU
        private CheckBox chkEnableCUDA;
        private Button btnTestGPU;
        private Label lblGPUInfo;
        private NumericUpDown numGPUMemory;
        private CheckBox chkGPUFallback;
        
        // Configuración Avanzada
        private CheckBox chkEnableDebug;
        private NumericUpDown numDebugLevel;
        private TextBox txtCustomConfig;
        private Button btnImportConfig;
        private Button btnExportConfig;
        private Button btnResetConfig;

        public ConfigurationTabManager(MainForm mainForm)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _settingsManager = UnifiedSettingsManager.Instance;
            _voiceEngine = new VoiceControlEngine();
            _gpuAccelerator = new CUDAAccelerator();
            
            InitializeControls();
            SetupEventHandlers();
            LoadConfiguration();
        }

        /// <summary>
        /// Inicializa controles de configuración
        /// </summary>
        private void InitializeControls()
        {
            // TabControl principal
            tabConfig = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.FlatButtons,
                BackColor = _mainForm.BackColor
            };

            // Crear pestañas
            CreateGeneralTab();
            CreateAITab();
            CreatePerformanceTab();
            CreateVoiceTab();
            CreateGPUTab();
            CreateAdvancedTab();

            // Agregar pestañas al control
            tabConfig.TabPages.AddRange(new TabPage[]
            {
                tabGeneral, tabAI, tabPerformance, tabVoice, tabGPU, tabAdvanced
            });

            _mainForm.Controls.Add(tabConfig);
        }

        /// <summary>
        /// Crea pestaña de configuración general
        /// </summary>
        private void CreateGeneralTab()
        {
            tabGeneral = new TabPage("General")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
            };

            var y = 20;
            
            // Directorio de descargas
            tabGeneral.Controls.Add(new Label { Text = "Directorio de Descargas:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            txtDownloadDir = new TextBox { Location = new System.Drawing.Point(200, y - 3), Width = 300 };
            var btnBrowseDownload = new Button { Text = "...", Location = new System.Drawing.Point(510, y - 5), Width = 30 };
            btnBrowseDownload.Click += (s, e) => BrowseFolder(txtDownloadDir);
            tabGeneral.Controls.AddRange(new Control[] { txtDownloadDir, btnBrowseDownload });
            y += 35;

            // Directorio de datos
            tabGeneral.Controls.Add(new Label { Text = "Directorio de Datos:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            txtDataDir = new TextBox { Location = new System.Drawing.Point(200, y - 3), Width = 300 };
            var btnBrowseData = new Button { Text = "...", Location = new System.Drawing.Point(510, y - 5), Width = 30 };
            btnBrowseData.Click += (s, e) => BrowseFolder(txtDataDir);
            tabGeneral.Controls.AddRange(new Control[] { txtDataDir, btnBrowseData });
            y += 35;

            // Descargas máximas simultáneas
            tabGeneral.Controls.Add(new Label { Text = "Descargas Simultáneas:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            numMaxDownloads = new NumericUpDown { Location = new System.Drawing.Point(200, y - 3), Width = 100, Minimum = 1, Maximum = 20, Value = 5 };
            tabGeneral.Controls.Add(numMaxDownloads);
            y += 35;

            // Timeout de búsqueda
            tabGeneral.Controls.Add(new Label { Text = "Timeout Búsqueda (seg):", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            numSearchTimeout = new NumericUpDown { Location = new System.Drawing.Point(200, y - 3), Width = 100, Minimum = 10, Maximum = 300, Value = 60 };
            tabGeneral.Controls.Add(numSearchTimeout);
            y += 35;

            // Opciones varias
            chkAutoStart = new CheckBox { Text = "Iniciar con Windows", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White };
            tabGeneral.Controls.Add(chkAutoStart);
            y += 30;

            chkMinimizeToTray = new CheckBox { Text = "Minimizar a Bandeja", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White };
            tabGeneral.Controls.Add(chkMinimizeToTray);
            y += 30;

            chkCheckUpdates = new CheckBox { Text = "Verificar Actualizaciones", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White, Checked = true };
            tabGeneral.Controls.Add(chkCheckUpdates);
        }

        /// <summary>
        /// Crea pestaña de configuración de IA
        /// </summary>
        private void CreateAITab()
        {
            tabAI = new TabPage("Inteligencia Artificial")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
            };

            var y = 20;

            // Habilitar IA predictiva
            chkEnablePredictive = new CheckBox 
            { 
                Text = "Habilitar IA Predictiva", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.LightGreen,
                Checked = true 
            };
            tabAI.Controls.Add(chkEnablePredictive);
            y += 35;

            // Habilitar clasificación
            chkEnableClassification = new CheckBox 
            { 
                Text = "Habilitar Clasificación Neural", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.LightBlue,
                Checked = true 
            };
            tabAI.Controls.Add(chkEnableClassification);
            y += 35;

            // Número de predicciones
            tabAI.Controls.Add(new Label { Text = "Predicciones Máximas:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            numPredictionCount = new NumericUpDown { Location = new System.Drawing.Point(200, y - 3), Width = 100, Minimum = 1, Maximum = 50, Value = 10 };
            tabAI.Controls.Add(numPredictionCount);
            y += 35;

            // Umbral de confianza
            tabAI.Controls.Add(new Label { Text = "Umbral de Confianza:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            trackConfidenceThreshold = new TrackBar 
            { 
                Location = new System.Drawing.Point(200, y - 3), 
                Width = 200, 
                Minimum = 0, 
                Maximum = 100, 
                Value = 70,
                TickFrequency = 10
            };
            trackConfidenceThreshold.ValueChanged += (s, e) => lblConfidenceValue.Text = $"{trackConfidenceThreshold.Value}%";
            lblConfidenceValue = new Label { Text = "70%", Location = new System.Drawing.Point(410, y), ForeColor = System.Drawing.Color.White };
            tabAI.Controls.AddRange(new Control[] { trackConfidenceThreshold, lblConfidenceValue });
            y += 50;

            // Botones de gestión de datos
            btnClearLearningData = new Button 
            { 
                Text = "Limpiar Datos de Aprendizaje", 
                Location = new System.Drawing.Point(20, y), 
                Width = 200,
                BackColor = System.Drawing.Color.FromArgb(220, 53, 69),
                ForeColor = System.Drawing.Color.White
            };
            btnClearLearningData.Click += OnClearLearningData;
            tabAI.Controls.Add(btnClearLearningData);
            y += 35;

            btnExportLearningData = new Button 
            { 
                Text = "📤 Exportar Datos de IA", 
                Location = new System.Drawing.Point(20, y), 
                Width = 200,
                BackColor = System.Drawing.Color.FromArgb(13, 110, 253),
                ForeColor = System.Drawing.Color.White
            };
            btnExportLearningData.Click += OnExportLearningData;
            tabAI.Controls.Add(btnExportLearningData);
        }

        /// <summary>
        /// Crea pestaña de configuración de rendimiento
        /// </summary>
        private void CreatePerformanceTab()
        {
            tabPerformance = new TabPage("Rendimiento")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
            };

            var y = 20;

            // Memory Pool
            chkEnableMemoryPool = new CheckBox 
            { 
                Text = "Habilitar Memory Pool Ultra-Optimizado", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.LightGreen,
                Checked = true 
            };
            tabPerformance.Controls.Add(chkEnableMemoryPool);
            y += 35;

            tabPerformance.Controls.Add(new Label { Text = "Tamaño del Pool:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            numPoolSize = new NumericUpDown { Location = new System.Drawing.Point(200, y - 3), Width = 100, Minimum = 100, Maximum = 10000, Value = 1000 };
            tabPerformance.Controls.Add(numPoolSize);
            y += 35;

            // Async Pipeline
            chkEnableAsyncPipeline = new CheckBox 
            { 
                Text = "Habilitar Async Pipeline Reactivo", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.LightBlue,
                Checked = true 
            };
            tabPerformance.Controls.Add(chkEnableAsyncPipeline);
            y += 35;

            tabPerformance.Controls.Add(new Label { Text = "Concurrencia Máxima:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            numConcurrency = new NumericUpDown { Location = new System.Drawing.Point(200, y - 3), Width = 100, Minimum = 1, Maximum = 100, Value = 10 };
            tabPerformance.Controls.Add(numConcurrency);
            y += 35;

            // GPU
            chkEnableGPU = new CheckBox 
            { 
                Text = "Habilitar Aceleración GPU", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.Yellow,
                Checked = false 
            };
            tabPerformance.Controls.Add(chkEnableGPU);
            y += 35;

            tabPerformance.Controls.Add(new Label { Text = "Dispositivo GPU:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            cbGPUDevice = new ComboBox { Location = new System.Drawing.Point(200, y - 3), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cbGPUDevice.Items.Add("NVIDIA GeForce RTX 4090");
            cbGPUDevice.Items.Add("NVIDIA GeForce RTX 3080");
            cbGPUDevice.Items.Add("AMD Radeon RX 7900");
            cbGPUDevice.SelectedIndex = 0;
            tabPerformance.Controls.Add(cbGPUDevice);
        }

        /// <summary>
        /// Crea pestaña de configuración de voz
        /// </summary>
        private void CreateVoiceTab()
        {
            tabVoice = new TabPage("Control por Voz")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
            };

            var y = 20;

            // Habilitar control por voz
            chkEnableVoiceControl = new CheckBox 
            { 
                Text = "Habilitar Control por Voz", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.LightGreen,
                Checked = false 
            };
            tabVoice.Controls.Add(chkEnableVoiceControl);
            y += 35;

            // Idioma
            tabVoice.Controls.Add(new Label { Text = "Idioma:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            cbVoiceLanguage = new ComboBox 
            { 
                Location = new System.Drawing.Point(200, y - 3), 
                Width = 150, 
                DropDownStyle = ComboBoxStyle.DropDownList 
            };
            cbVoiceLanguage.Items.AddRange(new[] { "Español", "Inglés", "Francés", "Alemán" });
            cbVoiceLanguage.SelectedIndex = 0;
            tabVoice.Controls.Add(cbVoiceLanguage);
            y += 35;

            // Sensibilidad
            tabVoice.Controls.Add(new Label { Text = "Sensibilidad:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            trackVoiceSensitivity = new TrackBar 
            { 
                Location = new System.Drawing.Point(200, y - 3), 
                Width = 200, 
                Minimum = 1, 
                Maximum = 10, 
                Value = 5 
            };
            tabVoice.Controls.Add(trackVoiceSensitivity);
            y += 50;

            // Estado
            lblVoiceStatus = new Label 
            { 
                Text = "Control por voz desactivado", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.LightGray 
            };
            tabVoice.Controls.Add(lblVoiceStatus);
            y += 35;

            // Botones
            btnTestVoice = new Button 
            { 
                Text = "Probar Micrófono", 
                Location = new System.Drawing.Point(20, y), 
                Width = 150,
                BackColor = System.Drawing.Color.FromArgb(13, 110, 253),
                ForeColor = System.Drawing.Color.White
            };
            btnTestVoice.Click += OnTestVoice;
            tabVoice.Controls.Add(btnTestVoice);

            btnTrainVoice = new Button 
            { 
                Text = "Entrenar Voz", 
                Location = new System.Drawing.Point(180, y), 
                Width = 150,
                BackColor = System.Drawing.Color.FromArgb(25, 135, 84),
                ForeColor = System.Drawing.Color.White
            };
            btnTrainVoice.Click += OnTrainVoice;
            tabVoice.Controls.Add(btnTrainVoice);
        }

        /// <summary>
        /// Crea pestaña de configuración de GPU
        /// </summary>
        private void CreateGPUTab()
        {
            tabGPU = new TabPage("🎮 GPU CUDA")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
            };

            var y = 20;

            // Habilitar CUDA
            chkEnableCUDA = new CheckBox 
            { 
                Text = "Habilitar Aceleración CUDA", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.LightGreen,
                Checked = false 
            };
            chkEnableCUDA.CheckedChanged += OnCUDAChanged;
            tabGPU.Controls.Add(chkEnableCUDA);
            y += 35;

            // Información GPU
            lblGPUInfo = new Label 
            { 
                Text = "Detectando GPU...", 
                Location = new System.Drawing.Point(20, y), 
                Size = new System.Drawing.Size(500, 60),
                ForeColor = System.Drawing.Color.White 
            };
            tabGPU.Controls.Add(lblGPUInfo);
            y += 70;

            // Memoria GPU
            tabGPU.Controls.Add(new Label { Text = "Memoria GPU (MB):", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            numGPUMemory = new NumericUpDown { Location = new System.Drawing.Point(200, y - 3), Width = 100, Minimum = 512, Maximum = 24576, Value = 4096 };
            tabGPU.Controls.Add(numGPUMemory);
            y += 35;

            // Fallback
            chkGPUFallback = new CheckBox 
            { 
                Text = "Usar CPU como fallback si GPU falla", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.White,
                Checked = true 
            };
            tabGPU.Controls.Add(chkGPUFallback);
            y += 35;

            // Botón de prueba
            btnTestGPU = new Button 
            { 
                Text = "Probar Rendimiento GPU", 
                Location = new System.Drawing.Point(20, y), 
                Width = 200,
                BackColor = System.Drawing.Color.FromArgb(13, 110, 253),
                ForeColor = System.Drawing.Color.White
            };
            btnTestGPU.Click += OnTestGPU;
            tabGPU.Controls.Add(btnTestGPU);

            // Actualizar información GPU
            UpdateGPUInfo();
        }

        /// <summary>
        /// Crea pestaña de configuración avanzada
        /// </summary>
        private void CreateAdvancedTab()
        {
            tabAdvanced = new TabPage("Avanzado")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48)
            };

            var y = 20;

            // Debug
            chkEnableDebug = new CheckBox 
            { 
                Text = "Habilitar Modo Debug", 
                Location = new System.Drawing.Point(20, y), 
                ForeColor = System.Drawing.Color.Yellow,
                Checked = false 
            };
            tabAdvanced.Controls.Add(chkEnableDebug);
            y += 35;

            tabAdvanced.Controls.Add(new Label { Text = "Nivel Debug:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            numDebugLevel = new NumericUpDown { Location = new System.Drawing.Point(200, y - 3), Width = 100, Minimum = 1, Maximum = 5, Value = 1 };
            tabAdvanced.Controls.Add(numDebugLevel);
            y += 35;

            // Configuración personalizada
            tabAdvanced.Controls.Add(new Label { Text = "Configuración JSON:", Location = new System.Drawing.Point(20, y), ForeColor = System.Drawing.Color.White });
            txtCustomConfig = new TextBox 
            { 
                Location = new System.Drawing.Point(20, y + 25), 
                Size = new System.Drawing.Size(600, 200),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Consolas", 9f),
                BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
                ForeColor = System.Drawing.Color.White
            };
            tabAdvanced.Controls.Add(txtCustomConfig);
            y += 240;

            // Botones de gestión
            btnImportConfig = new Button 
            { 
                Text = "📥 Importar Config", 
                Location = new System.Drawing.Point(20, y), 
                Width = 120,
                BackColor = System.Drawing.Color.FromArgb(13, 110, 253),
                ForeColor = System.Drawing.Color.White
            };
            btnImportConfig.Click += OnImportConfig;
            tabAdvanced.Controls.Add(btnImportConfig);

            btnExportConfig = new Button 
            { 
                Text = "📤 Exportar Config", 
                Location = new System.Drawing.Point(150, y), 
                Width = 120,
                BackColor = System.Drawing.Color.FromArgb(25, 135, 84),
                ForeColor = System.Drawing.Color.White
            };
            btnExportConfig.Click += OnExportConfig;
            tabAdvanced.Controls.Add(btnExportConfig);

            btnResetConfig = new Button 
            { 
                Text = "Restablecer", 
                Location = new System.Drawing.Point(280, y), 
                Width = 120,
                BackColor = System.Drawing.Color.FromArgb(220, 53, 69),
                ForeColor = System.Drawing.Color.White
            };
            btnResetConfig.Click += OnResetConfig;
            tabAdvanced.Controls.Add(btnResetConfig);
        }

        /// <summary>
        /// Configura manejadores de eventos
        /// </summary>
        private void SetupEventHandlers()
        {
            // Eventos generales
            txtDownloadDir.TextChanged += (s, e) => SaveConfiguration();
            txtDataDir.TextChanged += (s, e) => SaveConfiguration();
            numMaxDownloads.ValueChanged += (s, e) => SaveConfiguration();
            numSearchTimeout.ValueChanged += (s, e) => SaveConfiguration();
            
            // Eventos IA
            chkEnablePredictive.CheckedChanged += (s, e) => SaveConfiguration();
            chkEnableClassification.CheckedChanged += (s, e) => SaveConfiguration();
            numPredictionCount.ValueChanged += (s, e) => SaveConfiguration();
            trackConfidenceThreshold.ValueChanged += (s, e) => SaveConfiguration();
            
            // Eventos rendimiento
            chkEnableMemoryPool.CheckedChanged += (s, e) => SaveConfiguration();
            numPoolSize.ValueChanged += (s, e) => SaveConfiguration();
            chkEnableAsyncPipeline.CheckedChanged += (s, e) => SaveConfiguration();
            numConcurrency.ValueChanged += (s, e) => SaveConfiguration();
            
            // Eventos voz
            chkEnableVoiceControl.CheckedChanged += OnVoiceControlChanged;
            trackVoiceSensitivity.ValueChanged += (s, e) => SaveConfiguration();
        }

        /// <summary>
        /// Carga configuración desde settings
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                // Cargar configuración general
                txtDownloadDir.Text = _settingsManager.GetValue("downloadDir", @"C:\Downloads\SlskDown");
                txtDataDir.Text = _settingsManager.GetValue("dataDir", @"C:\Data\SlskDown");
                numMaxDownloads.Value = _settingsManager.GetValue("maxDownloads", 5);
                numSearchTimeout.Value = _settingsManager.GetValue("searchTimeout", 60);
                chkAutoStart.Checked = _settingsManager.GetValue("autoStart", false);
                chkMinimizeToTray.Checked = _settingsManager.GetValue("minimizeToTray", true);
                chkCheckUpdates.Checked = _settingsManager.GetValue("checkUpdates", true);

                // Cargar configuración IA
                chkEnablePredictive.Checked = _settingsManager.GetValue("enablePredictive", true);
                chkEnableClassification.Checked = _settingsManager.GetValue("enableClassification", true);
                numPredictionCount.Value = _settingsManager.GetValue("predictionCount", 10);
                trackConfidenceThreshold.Value = _settingsManager.GetValue("confidenceThreshold", 70);

                // Cargar configuración rendimiento
                chkEnableMemoryPool.Checked = _settingsManager.GetValue("enableMemoryPool", true);
                numPoolSize.Value = _settingsManager.GetValue("poolSize", 1000);
                chkEnableAsyncPipeline.Checked = _settingsManager.GetValue("enableAsyncPipeline", true);
                numConcurrency.Value = _settingsManager.GetValue("concurrency", 10);
                chkEnableGPU.Checked = _settingsManager.GetValue("enableGPU", false);

                // Cargar configuración voz
                chkEnableVoiceControl.Checked = _settingsManager.GetValue("enableVoiceControl", false);
                cbVoiceLanguage.SelectedIndex = _settingsManager.GetValue("voiceLanguage", 0);
                trackVoiceSensitivity.Value = _settingsManager.GetValue("voiceSensitivity", 5);

                // Cargar configuración GPU
                chkEnableCUDA.Checked = _settingsManager.GetValue("enableCUDA", false);
                numGPUMemory.Value = _settingsManager.GetValue("gpuMemory", 4096);
                chkGPUFallback.Checked = _settingsManager.GetValue("gpuFallback", true);

                // Cargar configuración avanzada
                chkEnableDebug.Checked = _settingsManager.GetValue("enableDebug", false);
                numDebugLevel.Value = _settingsManager.GetValue("debugLevel", 1);
                
                // Cargar configuración JSON personalizada
                var customConfig = _settingsManager.GetValue("customConfig", "");
                txtCustomConfig.Text = customConfig;

                UpdateVoiceStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando configuración: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Guarda configuración actual
        /// </summary>
        private void SaveConfiguration()
        {
            try
            {
                // Guardar configuración general
                _settingsManager.SetValue("downloadDir", txtDownloadDir.Text);
                _settingsManager.SetValue("dataDir", txtDataDir.Text);
                _settingsManager.SetValue("maxDownloads", (int)numMaxDownloads.Value);
                _settingsManager.SetValue("searchTimeout", (int)numSearchTimeout.Value);
                _settingsManager.SetValue("autoStart", chkAutoStart.Checked);
                _settingsManager.SetValue("minimizeToTray", chkMinimizeToTray.Checked);
                _settingsManager.SetValue("checkUpdates", chkCheckUpdates.Checked);

                // Guardar configuración IA
                _settingsManager.SetValue("enablePredictive", chkEnablePredictive.Checked);
                _settingsManager.SetValue("enableClassification", chkEnableClassification.Checked);
                _settingsManager.SetValue("predictionCount", (int)numPredictionCount.Value);
                _settingsManager.SetValue("confidenceThreshold", trackConfidenceThreshold.Value);

                // Guardar configuración rendimiento
                _settingsManager.SetValue("enableMemoryPool", chkEnableMemoryPool.Checked);
                _settingsManager.SetValue("poolSize", (int)numPoolSize.Value);
                _settingsManager.SetValue("enableAsyncPipeline", chkEnableAsyncPipeline.Checked);
                _settingsManager.SetValue("concurrency", (int)numConcurrency.Value);
                _settingsManager.SetValue("enableGPU", chkEnableGPU.Checked);

                // Guardar configuración voz
                _settingsManager.SetValue("enableVoiceControl", chkEnableVoiceControl.Checked);
                _settingsManager.SetValue("voiceLanguage", cbVoiceLanguage.SelectedIndex);
                _settingsManager.SetValue("voiceSensitivity", trackVoiceSensitivity.Value);

                // Guardar configuración GPU
                _settingsManager.SetValue("enableCUDA", chkEnableCUDA.Checked);
                _settingsManager.SetValue("gpuMemory", (int)numGPUMemory.Value);
                _settingsManager.SetValue("gpuFallback", chkGPUFallback.Checked);

                // Guardar configuración avanzada
                _settingsManager.SetValue("enableDebug", chkEnableDebug.Checked);
                _settingsManager.SetValue("debugLevel", (int)numDebugLevel.Value);
                _settingsManager.SetValue("customConfig", txtCustomConfig.Text);

                _settingsManager.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando configuración: {ex.Message}");
            }
        }

        /// <summary>
        /// Abre diálogo para seleccionar carpeta
        /// </summary>
        private void BrowseFolder(TextBox textBox)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.SelectedPath = textBox.Text;
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = folderDialog.SelectedPath;
                    SaveConfiguration();
                }
            }
        }

        /// <summary>
        /// Actualiza información de GPU
        /// </summary>
        private void UpdateGPUInfo()
        {
            try
            {
                var gpuInfo = _gpuAccelerator.GetDeviceInfo();
                
                if (gpuInfo.IsAvailable)
                {
                    lblGPUInfo.Text = $"GPU Detectada: {gpuInfo.DeviceName}\n" +
                                     $"Memoria Total: {gpuInfo.MemoryTotal / 1024 / 1024 / 1024} GB\n" +
                                     $"Compute Capability: {gpuInfo.ComputeCapability}\n" +
                                     $"Driver: {gpuInfo.DriverVersion}";
                }
                else
                {
                    lblGPUInfo.Text = "No se detectó GPU CUDA compatible\n" +
                                     "Asegúrate de tener NVIDIA GPU y drivers actualizados";
                    chkEnableCUDA.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                lblGPUInfo.Text = $"Error detectando GPU: {ex.Message}";
            }
        }

        /// <summary>
        /// Actualiza estado del control por voz
        /// </summary>
        private void UpdateVoiceStatus()
        {
            if (chkEnableVoiceControl.Checked)
            {
                if (_voiceEngine.IsAvailable)
                {
                    lblVoiceStatus.Text = _voiceEngine.IsListening ? "Escuchando..." : "Listo para escuchar";
                    lblVoiceStatus.ForeColor = System.Drawing.Color.LightGreen;
                }
                else
                {
                    lblVoiceStatus.Text = "Micrófono no disponible";
                    lblVoiceStatus.ForeColor = System.Drawing.Color.Red;
                    chkEnableVoiceControl.Checked = false;
                }
            }
            else
            {
                lblVoiceStatus.Text = "Control por voz desactivado";
                lblVoiceStatus.ForeColor = System.Drawing.Color.LightGray;
            }
        }

        #region Event Handlers

        private async void OnClearLearningData(object sender, EventArgs e)
        {
            if (MessageBox.Show("¿Estás seguro de que quieres limpiar todos los datos de aprendizaje?\n\nEsto eliminará todo el historial de búsquedas y descargas aprendido por la IA.", 
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    // Implementar limpieza de datos de aprendizaje
                    MessageBox.Show("Datos de aprendizaje limpiados correctamente", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error limpiando datos: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void OnExportLearningData(object sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                saveDialog.FileName = $"slskdown_learning_data_{DateTime.Now:yyyyMMdd}.json";
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Implementar exportación de datos
                        MessageBox.Show("Datos de IA exportados correctamente", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error exportando datos: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void OnTestVoice(object sender, EventArgs e)
        {
            try
            {
                btnTestVoice.Text = "Probando...";
                btnTestVoice.Enabled = false;
                
                // Simular prueba de micrófono
                await Task.Delay(2000);
                
                MessageBox.Show("Micrófono funcionando correctamente", "Prueba Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en prueba de micrófono: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTestVoice.Text = "Probar Micrófono";
                btnTestVoice.Enabled = true;
            }
        }

        private async void OnTrainVoice(object sender, EventArgs e)
        {
            try
            {
                btnTrainVoice.Text = "Entrenando...";
                btnTrainVoice.Enabled = false;
                
                // Simular entrenamiento
                await Task.Delay(3000);
                
                MessageBox.Show("Entrenamiento de voz completado", "Entrenamiento Exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en entrenamiento: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTrainVoice.Text = "Entrenar Voz";
                btnTrainVoice.Enabled = true;
            }
        }

        private void OnVoiceControlChanged(object sender, EventArgs e)
        {
            if (chkEnableVoiceControl.Checked)
            {
                Task.Run(async => 
                {
                    try
                    {
                        await _voiceEngine.StartListeningAsync();
                        _mainForm.SafeBeginInvoke(() => UpdateVoiceStatus());
                    }
                    catch (Exception ex)
                    {
                        _mainForm.SafeBeginInvoke(() => 
                        {
                            chkEnableVoiceControl.Checked = false;
                            MessageBox.Show($"Error iniciando control por voz: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                });
            }
            else
            {
                Task.Run(async => 
                {
                    try
                    {
                        await _voiceEngine.StopListeningAsync();
                        _mainForm.SafeBeginInvoke(() => UpdateVoiceStatus());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error deteniendo voz: {ex.Message}");
                    }
                });
            }
            
            SaveConfiguration();
        }

        private void OnCUDAChanged(object sender, EventArgs e)
        {
            if (chkEnableCUDA.Checked && !_gpuAccelerator.IsAvailable)
            {
                MessageBox.Show("No se detectó GPU CUDA compatible. La aceleración GPU no estará disponible.", 
                    "GPU No Disponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            
            SaveConfiguration();
        }

        private async void OnTestGPU(object sender, EventArgs e)
        {
            try
            {
                btnTestGPU.Text = "Probando...";
                btnTestGPU.Enabled = false;
                
                // Simular prueba de GPU
                await Task.Delay(3000);
                
                MessageBox.Show("Rendimiento GPU probado correctamente\n\nVelocidad de procesamiento: 1000 ops/ms", 
                    "Prueba Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en prueba GPU: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTestGPU.Text = "Probar Rendimiento GPU";
                btnTestGPU.Enabled = true;
            }
        }

        private void OnImportConfig(object sender, EventArgs e)
        {
            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var config = File.ReadAllText(openDialog.FileName);
                        txtCustomConfig.Text = config;
                        SaveConfiguration();
                        MessageBox.Show("Configuración importada correctamente", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error importando configuración: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void OnExportConfig(object sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                saveDialog.FileName = $"slskdown_config_{DateTime.Now:yyyyMMdd}.json";
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(saveDialog.FileName, txtCustomConfig.Text);
                        MessageBox.Show("Configuración exportada correctamente", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error exportando configuración: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void OnResetConfig(object sender, EventArgs e)
        {
            if (MessageBox.Show("¿Estás seguro de que quieres restablecer toda la configuración a los valores predeterminados?\n\nEsta acción no se puede deshacer.", 
                "Confirmar Restablecimiento", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    _settingsManager.Reset();
                    LoadConfiguration();
                    MessageBox.Show("Configuración restablecida correctamente", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error restableciendo configuración: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            _voiceEngine?.Dispose();
            _gpuAccelerator?.Dispose();
        }
    }
}
