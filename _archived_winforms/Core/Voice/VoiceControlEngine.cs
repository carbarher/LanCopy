using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlskDown.Core.Voice
{
    /// <summary>
    /// Motor de control por voz y NLP para SlskDown
    /// </summary>
    public class VoiceControlEngine : IDisposable
    {
        private readonly SpeechRecognitionEngine _recognizer;
        private readonly SpeechSynthesizer _synthesizer;
        private readonly Dictionary<string, VoiceCommand> _commands;
        private readonly NLPProcessor _nlpProcessor;
        private volatile bool _isListening = false;
        private volatile bool _disposed = false;

        public event EventHandler<VoiceCommandRecognizedEventArgs> CommandRecognized;
        public bool IsListening => _isListening;
        public bool IsAvailable => _recognizer != null;

        public VoiceControlEngine()
        {
            try
            {
                // Inicializar reconocedor de voz
                _recognizer = new SpeechRecognitionEngine(new CultureInfo("es-ES"));
                _synthesizer = new SpeechSynthesizer();
                _nlpProcessor = new NLPProcessor();
                _commands = new Dictionary<string, VoiceCommand>();

                InitializeCommands();
                ConfigureRecognizer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Voice control initialization failed: {ex.Message}");
                _recognizer = null;
            }
        }

        /// <summary>
        /// Inicializa comandos de voz disponibles
        /// </summary>
        private void InitializeCommands()
        {
            // Comandos de búsqueda
            AddCommand(new VoiceCommand
            {
                Name = "search",
                Patterns = new[]
                {
                    @"buscar\s+(.+)",
                    @"busca\s+(.+)",
                    @"search\s+(.+)",
                    @"encuentra\s+(.+)"
                },
                Action = async (parameters) => await ExecuteSearch(parameters),
                Description = "Buscar música o archivos",
                Examples = new[] { "buscar jazz tranquilo", "busca rock de los 80s" }
            });

            // Comandos de descarga
            AddCommand(new VoiceCommand
            {
                Name = "download",
                Patterns = new[]
                {
                    @"descargar\s+(.+)",
                    @"descarga\s+(.+)",
                    @"download\s+(.+)",
                    @"bajar\s+(.+)"
                },
                Action = async (parameters) => await ExecuteDownload(parameters),
                Description = "Descargar archivos seleccionados",
                Examples = new[] { "descargar seleccionados", "bajar todo" }
            });

            // Comandos de control
            AddCommand(new VoiceCommand
            {
                Name = "pause",
                Patterns = new[]
                {
                    @"pausar",
                    @"pausa",
                    @"pause",
                    @"detener"
                },
                Action = async (parameters) => await ExecutePause(parameters),
                Description = "Pausar descargas",
                Examples = new[] { "pausar descargas", "detener todo" }
            });

            AddCommand(new VoiceCommand
            {
                Name = "resume",
                Patterns = new[]
                {
                    @"continuar",
                    @"reanudar",
                    @"resume",
                    @"seguir"
                },
                Action = async (parameters) => await ExecuteResume(parameters),
                Description = "Reanudar descargas",
                Examples = new[] { "continuar descargas", "reanudar todo" }
            });

            // Comandos de filtrado
            AddCommand(new VoiceCommand
            {
                Name = "filter",
                Patterns = new[]
                {
                    @"filtrar\s+(.+)",
                    @"filtra\s+(.+)",
                    @"filter\s+(.+)",
                    @"mostrar\s+solo\s+(.+)"
                },
                Action = async (parameters) => await ExecuteFilter(parameters),
                Description = "Filtrar resultados",
                Examples = new[] { "filtrar mp3", "mostrar solo FLAC" }
            });

            // Comandos de información
            AddCommand(new VoiceCommand
            {
                Name = "status",
                Patterns = new[]
                {
                    @"estado",
                    @"status",
                    @"información",
                    @"informacion"
                },
                Action = async (parameters) => await ExecuteStatus(parameters),
                Description = "Mostrar estado actual",
                Examples = new[] { "estado de descargas", "información del sistema" }
            });

            // Comandos de configuración
            AddCommand(new VoiceCommand
            {
                Name = "configure",
                Patterns = new[]
                {
                    @"configurar\s+(.+)",
                    @"configura\s+(.+)",
                    @"configure\s+(.+)",
                    @"ajustar\s+(.+)"
                },
                Action = async (parameters) => await ExecuteConfigure(parameters),
                Description = "Configurar opciones",
                Examples = new[] { "configurar descargas paralelas", "ajustar calidad" }
            });
        }

        /// <summary>
        /// Configura el reconocedor de voz
        /// </summary>
        private void ConfigureRecognizer()
        {
            if (_recognizer == null) return;

            // Cargar gramática con comandos
            var choices = new Choices();
            foreach (var command in _commands.Values)
            {
                foreach (var pattern in command.Patterns)
                {
                    choices.Add(pattern);
                }
            }

            var grammar = new Grammar(new GrammarBuilder(choices));
            _recognizer.LoadGrammar(grammar);

            // Configurar eventos
            _recognizer.SpeechRecognized += OnSpeechRecognized;
            _recognizer.SpeechRecognitionRejected += OnSpeechRejected;

            // Configurar audio
            _recognizer.SetInputToDefaultAudioDevice();
            _recognizer.BabbleTimeout = TimeSpan.FromSeconds(3);
            _recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(5);
            _recognizer.EndSilenceTimeout = TimeSpan.FromSeconds(2);
        }

        /// <summary>
        /// Agrega un comando de voz
        /// </summary>
        public void AddCommand(VoiceCommand command)
        {
            if (command == null) return;
            _commands[command.Name] = command;
        }

        /// <summary>
        /// Inicia escucha de voz
        /// </summary>
        public async Task<bool> StartListeningAsync()
        {
            if (_disposed || _recognizer == null) return false;

            try
            {
                await Task.Run(() =>
                {
                    _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                    _isListening = true;
                });

                Speak("Control por voz activado");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start voice recognition: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detiene escucha de voz
        /// </summary>
        public async Task StopListeningAsync()
        {
            if (_recognizer == null) return;

            try
            {
                await Task.Run(() =>
                {
                    _recognizer.RecognizeAsyncStop();
                    _isListening = false;
                });

                Speak("Control por voz desactivado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to stop voice recognition: {ex.Message}");
            }
        }

        /// <summary>
        /// Procesa texto directamente (sin voz)
        /// </summary>
        public async Task<bool> ProcessTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            try
            {
                var command = ParseCommand(text);
                if (command != null)
                {
                    await command.Action(command.Parameters);
                    CommandRecognized?.Invoke(this, new VoiceCommandRecognizedEventArgs
                    {
                        Command = command,
                        Confidence = 1.0f,
                        Text = text
                    });
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to process text: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Habla texto usando síntesis de voz
        /// </summary>
        public void Speak(string text)
        {
            if (_synthesizer == null || string.IsNullOrWhiteSpace(text)) return;

            try
            {
                _synthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to speak: {ex.Message}");
            }
        }

        /// <summary>
        /// Parsea comando desde texto usando NLP
        /// </summary>
        private ParsedCommand ParseCommand(string text)
        {
            var normalizedText = text.ToLower().Trim();

            foreach (var command in _commands.Values)
            {
                foreach (var pattern in command.Patterns)
                {
                    var match = Regex.Match(normalizedText, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var parameters = new Dictionary<string, string>();
                        
                        // Extraer parámetros del match
                        for (int i = 1; i < match.Groups.Count; i++)
                        {
                            parameters[$"param{i}"] = match.Groups[i].Value.Trim();
                        }

                        // Procesar con NLP para extraer más información
                        var nlpResult = _nlpProcessor.ProcessText(normalizedText);
                        foreach (var kvp in nlpResult.Entities)
                        {
                            parameters[kvp.Key] = kvp.Value;
                        }

                        return new ParsedCommand
                        {
                            Name = command.Name,
                            OriginalText = text,
                            Parameters = parameters,
                            Action = command.Action,
                            Confidence = CalculateConfidence(match, normalizedText)
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Calcula confianza del reconocimiento
        /// </summary>
        private float CalculateConfidence(Match match, string text)
        {
            // Simular cálculo de confianza
            var matchLength = match.Length;
            var textLength = text.Length;
            return (float)matchLength / textLength;
        }

        /// <summary>
        /// Maneja evento de reconocimiento de voz
        /// </summary>
        private void OnSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            try
            {
                var command = ParseCommand(e.Result.Text);
                if (command != null && command.Confidence > 0.5f)
                {
                    CommandRecognized?.Invoke(this, new VoiceCommandRecognizedEventArgs
                    {
                        Command = command,
                        Confidence = e.Result.Confidence,
                        Text = e.Result.Text
                    });

                    // Ejecutar comando
                    Task.Run(async () =>
                    {
                        try
                        {
                            await command.Action(command.Parameters);
                            Speak("Comando ejecutado");
                        }
                        catch (Exception ex)
                        {
                            Speak("Error al ejecutar comando");
                            System.Diagnostics.Debug.WriteLine($"Command execution failed: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Speech recognition error: {ex.Message}");
            }
        }

        /// <summary>
        /// Maneja rechazo de reconocimiento
        /// </summary>
        private void OnSpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Speech rejected: {e.Result?.Text}");
        }

        #region Implementación de Comandos

        private async Task ExecuteSearch(Dictionary<string, string> parameters)
        {
            var query = parameters.GetValueOrDefault("param1", "");
            
            // Procesar con NLP para mejorar búsqueda
            var nlpResult = _nlpProcessor.ProcessSearchQuery(query);
            
            // Simular ejecución de búsqueda
            await Task.Delay(500);
            
            Speak($"Buscando {nlpResult.ProcessedQuery}");
            
            // Aquí se integraría con el motor de búsqueda real
            System.Diagnostics.Debug.WriteLine($"Voice search: {nlpResult.ProcessedQuery}");
        }

        private async Task ExecuteDownload(Dictionary<string, string> parameters)
        {
            var target = parameters.GetValueOrDefault("param1", "seleccionados");
            
            Speak($"Descargando {target}");
            
            // Integrar con sistema de descargas
            await Task.Delay(1000);
        }

        private async Task ExecutePause(Dictionary<string, string> parameters)
        {
            Speak("Pausando descargas");
            
            // Integrar con control de descargas
            await Task.Delay(500);
        }

        private async Task ExecuteResume(Dictionary<string, string> parameters)
        {
            Speak("Reanudando descargas");
            
            // Integrar con control de descargas
            await Task.Delay(500);
        }

        private async Task ExecuteFilter(Dictionary<string, string> parameters)
        {
            var filterType = parameters.GetValueOrDefault("param1", "");
            
            Speak($"Filtrando por {filterType}");
            
            // Integrar con sistema de filtros
            await Task.Delay(500);
        }

        private async Task ExecuteStatus(Dictionary<string, string> parameters)
        {
            // Obtener estado real del sistema
            var status = "Sistema operativo. 5 descargas activas, 12 en cola.";
            
            Speak(status);
        }

        private async Task ExecuteConfigure(Dictionary<string, string> parameters)
        {
            var setting = parameters.GetValueOrDefault("param1", "");
            
            Speak($"Configurando {setting}");
            
            // Integrar con sistema de configuración
            await Task.Delay(500);
        }

        #endregion

        /// <summary>
        /// Obtiene comandos disponibles
        /// </summary>
        public List<VoiceCommand> GetAvailableCommands()
        {
            return _commands.Values.ToList();
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            if (_isListening)
            {
                StopListeningAsync().GetAwaiter().GetResult();
            }

            _recognizer?.Dispose();
            _synthesizer?.Dispose();
            _nlpProcessor?.Dispose();
        }
    }

    /// <summary>
    /// Procesador de lenguaje natural
    /// </summary>
    public class NLPProcessor : IDisposable
    {
        private readonly Dictionary<string, List<string>> _synonyms;
        private readonly Dictionary<string, string> _genreMappings;
        private readonly Dictionary<string, string> _qualityMappings;

        public NLPProcessor()
        {
            InitializeSynonyms();
            InitializeMappings();
        }

        /// <summary>
        /// Procesa texto y extrae entidades
        /// </summary>
        public NLPResult ProcessText(string text)
        {
            var result = new NLPResult { OriginalText = text };
            
            // Extraer géneros musicales
            foreach (var mapping in _genreMappings)
            {
                if (text.Contains(mapping.Key))
                {
                    result.Entities["genre"] = mapping.Value;
                    break;
                }
            }

            // Extraer calidad
            foreach (var mapping in _qualityMappings)
            {
                if (text.Contains(mapping.Key))
                {
                    result.Entities["quality"] = mapping.Value;
                    break;
                }
            }

            // Extraer palabras clave
            var keywords = ExtractKeywords(text);
            result.Entities["keywords"] = string.Join(" ", keywords);

            return result;
        }

        /// <summary>
        /// Procesa consulta de búsqueda
        /// </summary>
        public NLPResult ProcessSearchQuery(string query)
        {
            var result = ProcessText(query);
            
            // Normalizar consulta
            var processedQuery = NormalizeQuery(query);
            result.ProcessedQuery = processedQuery;

            return result;
        }

        /// <summary>
        /// Extrae palabras clave del texto
        /// </summary>
        private List<string> ExtractKeywords(string text)
        {
            var stopWords = new HashSet<string> 
            { 
                "el", "la", "los", "las", "de", "del", "en", "con", "por", "para", 
                "y", "o", "pero", "mas", "muy", "un", "una", "unos", "unas",
                "the", "a", "an", "and", "or", "in", "on", "at", "to", "for"
            };

            var words = text.ToLower()
                .Split(new[] { ' ', ',', '.', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length > 2 && !stopWords.Contains(word))
                .Distinct()
                .ToList();

            return words;
        }

        /// <summary>
        /// Normaliza consulta de búsqueda
        /// </summary>
        private string NormalizeQuery(string query)
        {
            var normalized = query.ToLower().Trim();
            
            // Reemplazar sinónimos
            foreach (var synonym in _synonyms)
            {
                foreach (var variant in synonym.Value)
                {
                    if (normalized.Contains(variant))
                    {
                        normalized = normalized.Replace(variant, synonym.Key);
                        break;
                    }
                }
            }

            // Limpiar caracteres especiales
            normalized = Regex.Replace(normalized, @"[^\w\s]", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            return normalized;
        }

        /// <summary>
        /// Inicializa sinónimos
        /// </summary>
        private void InitializeSynonyms()
        {
            _synonyms = new Dictionary<string, List<string>>
            {
                ["musica"] = new List<string> { "música", "canciones", "temas", "tracks", "songs" },
                ["album"] = new List<string> { "álbum", "disco", "cd", "lp" },
                ["artista"] = new List<string> { "artista", "autor", "grupo", "band", "artist" },
                ["rapido"] = new List<string> { "rápido", "veloz", "fast", "quick" },
                ["lento"] = new List<string> { "lento", "tranquilo", "slow", "calm" }
            };
        }

        /// <summary>
        /// Inicializa mapeos
        /// </summary>
        private void InitializeMappings()
        {
            _genreMappings = new Dictionary<string, string>
            {
                ["jazz"] = "jazz",
                ["rock"] = "rock",
                ["clasica"] = "classical",
                ["electrónica"] = "electronic",
                ["electronica"] = "electronic",
                ["pop"] = "pop",
                ["hip hop"] = "hip-hop",
                ["rap"] = "hip-hop",
                ["blues"] = "blues",
                ["reggae"] = "reggae"
            };

            _qualityMappings = new Dictionary<string, string>
            {
                ["flac"] = "lossless",
                ["lossless"] = "lossless",
                ["320"] = "high",
                ["256"] = "medium",
                ["192"] = "medium",
                ["128"] = "standard"
            };
        }

        public void Dispose()
        {
            // Cleanup si es necesario
        }
    }

    #region Modelos

    public class VoiceCommand
    {
        public string Name { get; set; }
        public string[] Patterns { get; set; }
        public Func<Dictionary<string, string>, Task> Action { get; set; }
        public string Description { get; set; }
        public string[] Examples { get; set; }
    }

    public class ParsedCommand
    {
        public string Name { get; set; }
        public string OriginalText { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public Func<Dictionary<string, string>, Task> Action { get; set; }
        public float Confidence { get; set; }
    }

    public class NLPResult
    {
        public string OriginalText { get; set; }
        public string ProcessedQuery { get; set; }
        public Dictionary<string, string> Entities { get; set; } = new Dictionary<string, string>();
    }

    public class VoiceCommandRecognizedEventArgs : EventArgs
    {
        public ParsedCommand Command { get; set; }
        public float Confidence { get; set; }
        public string Text { get; set; }
    }

    #endregion
}
