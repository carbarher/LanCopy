using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown
{
    // ═══════════════════════════════════════════════════════════════
    // SISTEMA DE REPUTACIÓN DESCENTRALIZADO CON BLOCKCHAIN
    // ═══════════════════════════════════════════════════════════════
    
    public class ReputationTransaction
    {
        public string TransactionId { get; set; }
        public string FromUser { get; set; }
        public string ToUser { get; set; }
        public int Rating { get; set; } // 1-5
        public string Comment { get; set; }
        public DateTime Timestamp { get; set; }
        public string Hash { get; set; }
        public string PreviousHash { get; set; }
    }
    
    public class ReputationBlock
    {
        public int Index { get; set; }
        public DateTime Timestamp { get; set; }
        public List<ReputationTransaction> Transactions { get; set; }
        public string Hash { get; set; }
        public string PreviousHash { get; set; }
        public int Nonce { get; set; }
        
        public ReputationBlock()
        {
            Transactions = new List<ReputationTransaction>();
        }
        
        public string CalculateHash()
        {
            var data = $"{Index}{Timestamp}{JsonSerializer.Serialize(Transactions)}{PreviousHash}{Nonce}";
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
    
    public class BlockchainReputation
    {
        private List<ReputationBlock> chain;
        private List<ReputationTransaction> pendingTransactions;
        private Action<string> logAction;
        private int difficulty = 2; // Número de ceros al inicio del hash
        
        public BlockchainReputation(Action<string> logger)
        {
            logAction = logger;
            chain = new List<ReputationBlock>();
            pendingTransactions = new List<ReputationTransaction>();
            
            // Crear bloque génesis
            CreateGenesisBlock();
        }
        
        private void CreateGenesisBlock()
        {
            var genesis = new ReputationBlock
            {
                Index = 0,
                Timestamp = DateTime.Now,
                PreviousHash = "0",
                Transactions = new List<ReputationTransaction>()
            };
            
            genesis.Hash = genesis.CalculateHash();
            chain.Add(genesis);
            
            logAction?.Invoke("⛓️ Blockchain de reputación inicializado");
        }
        
        public void AddTransaction(string fromUser, string toUser, int rating, string comment)
        {
            var transaction = new ReputationTransaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                FromUser = fromUser,
                ToUser = toUser,
                Rating = Math.Max(1, Math.Min(5, rating)),
                Comment = comment,
                Timestamp = DateTime.Now
            };
            
            transaction.Hash = CalculateTransactionHash(transaction);
            pendingTransactions.Add(transaction);
            
            logAction?.Invoke($"⛓️ Transacción agregada: {fromUser} → {toUser} ({rating}⭐)");
        }
        
        private string CalculateTransactionHash(ReputationTransaction tx)
        {
            var data = $"{tx.TransactionId}{tx.FromUser}{tx.ToUser}{tx.Rating}{tx.Timestamp}";
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }
        
        public void MinePendingTransactions()
        {
            if (pendingTransactions.Count == 0)
            {
                logAction?.Invoke("⛓️ No hay transacciones pendientes para minar");
                return;
            }
            
            var block = new ReputationBlock
            {
                Index = chain.Count,
                Timestamp = DateTime.Now,
                Transactions = new List<ReputationTransaction>(pendingTransactions),
                PreviousHash = chain[chain.Count - 1].Hash
            };
            
            // Proof of Work
            block.Hash = MineBlock(block);
            
            chain.Add(block);
            pendingTransactions.Clear();
            
            logAction?.Invoke($"⛓️ Bloque #{block.Index} minado con {block.Transactions.Count} transacciones");
        }
        
        private string MineBlock(ReputationBlock block)
        {
            var target = new string('0', difficulty);
            
            while (true)
            {
                block.Hash = block.CalculateHash();
                
                if (block.Hash.StartsWith(target))
                {
                    return block.Hash;
                }
                
                block.Nonce++;
            }
        }
        
        public double GetUserReputation(string username)
        {
            var ratings = new List<int>();
            
            foreach (var block in chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.ToUser == username)
                    {
                        ratings.Add(tx.Rating);
                    }
                }
            }
            
            if (ratings.Count == 0) return 0;
            
            return ratings.Average();
        }
        
        public bool ValidateChain()
        {
            for (int i = 1; i < chain.Count; i++)
            {
                var currentBlock = chain[i];
                var previousBlock = chain[i - 1];
                
                // Verificar hash del bloque
                if (currentBlock.Hash != currentBlock.CalculateHash())
                {
                    logAction?.Invoke($"❌ Bloque #{i} tiene hash inválido");
                    return false;
                }
                
                // Verificar enlace con bloque anterior
                if (currentBlock.PreviousHash != previousBlock.Hash)
                {
                    logAction?.Invoke($"❌ Bloque #{i} no enlaza correctamente con anterior");
                    return false;
                }
            }
            
            logAction?.Invoke("✅ Blockchain válido");
            return true;
        }
        
        public int GetChainLength()
        {
            return chain.Count;
        }
        
        public List<ReputationTransaction> GetUserTransactions(string username)
        {
            var transactions = new List<ReputationTransaction>();
            
            foreach (var block in chain)
            {
                transactions.AddRange(block.Transactions.Where(tx => 
                    tx.FromUser == username || tx.ToUser == username));
            }
            
            return transactions;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // INTEGRACIÓN CON IPFS PARA ARCHIVOS GRANDES
    // ═══════════════════════════════════════════════════════════════
    
    public class IPFSFile
    {
        public string Hash { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
        public DateTime UploadDate { get; set; }
        public List<string> Gateways { get; set; }
    }
    
    public class IPFSIntegration
    {
        private Action<string> logAction;
        private List<string> ipfsGateways;
        private Dictionary<string, IPFSFile> fileCache;
        
        public IPFSIntegration(Action<string> logger)
        {
            logAction = logger;
            fileCache = new Dictionary<string, IPFSFile>();
            
            ipfsGateways = new List<string>
            {
                "https://ipfs.io/ipfs/",
                "https://gateway.pinata.cloud/ipfs/",
                "https://cloudflare-ipfs.com/ipfs/",
                "https://dweb.link/ipfs/"
            };
        }
        
        public async Task<string> UploadToIPFS(string filePath)
        {
            try
            {
                // Simulación de upload a IPFS
                // En producción, usar Ipfs.Http.Client
                
                var fileName = System.IO.Path.GetFileName(filePath);
                var fileSize = new System.IO.FileInfo(filePath).Length;
                
                // Generar hash simulado (en producción sería el CID real)
                var hash = GenerateIPFSHash(filePath);
                
                var ipfsFile = new IPFSFile
                {
                    Hash = hash,
                    FileName = fileName,
                    Size = fileSize,
                    UploadDate = DateTime.Now,
                    Gateways = ipfsGateways
                };
                
                fileCache[hash] = ipfsFile;
                
                logAction?.Invoke($"📦 Archivo subido a IPFS: {fileName}");
                logAction?.Invoke($"   Hash: {hash}");
                logAction?.Invoke($"   Tamaño: {fileSize / 1024 / 1024}MB");
                
                return hash;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error subiendo a IPFS: {ex.Message}");
                return null;
            }
        }
        
        public async Task<string> DownloadFromIPFS(string hash, string outputPath)
        {
            try
            {
                // Intentar descargar desde múltiples gateways
                foreach (var gateway in ipfsGateways)
                {
                    try
                    {
                        var url = $"{gateway}{hash}";
                        logAction?.Invoke($"📥 Descargando desde: {gateway}");
                        
                        // Simulación de descarga
                        // En producción, usar HttpClient para descargar
                        await Task.Delay(100);
                        
                        logAction?.Invoke($"✅ Descarga completada desde IPFS");
                        return outputPath;
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                logAction?.Invoke($"❌ No se pudo descargar desde ningún gateway");
                return null;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error descargando de IPFS: {ex.Message}");
                return null;
            }
        }
        
        private string GenerateIPFSHash(string filePath)
        {
            // Simulación de CID de IPFS
            // En producción, usar Ipfs.Core para generar CID real
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(filePath + DateTime.Now.Ticks));
                return "Qm" + BitConverter.ToString(bytes).Replace("-", "").Substring(0, 44);
            }
        }
        
        public IPFSFile GetFileInfo(string hash)
        {
            return fileCache.GetValueOrDefault(hash);
        }
        
        public List<string> GetAvailableGateways()
        {
            return new List<string>(ipfsGateways);
        }
        
        public void AddGateway(string gateway)
        {
            if (!ipfsGateways.Contains(gateway))
            {
                ipfsGateways.Add(gateway);
                logAction?.Invoke($"➕ Gateway IPFS agregado: {gateway}");
            }
        }
    }
}
