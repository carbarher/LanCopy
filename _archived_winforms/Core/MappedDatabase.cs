// <copyright file="MappedDatabase.cs" company="SlskDown">
//     Base de datos con memory-mapped files para acceso ultra-rápido
// </copyright>

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace SlskDown.Core
{
    /// <summary>
    /// Base de datos usando memory-mapped files para acceso O(1).
    /// Inspirado en el sistema de Database de Nicotine+ con mmap.
    /// </summary>
    public class MappedDatabase : IDisposable
    {
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _accessor;
        private readonly string _filePath;
        private bool _disposed = false;

        public bool IsOpen => _mmf != null;
        public long Size => _accessor?.Capacity ?? 0;

        public MappedDatabase(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        /// <summary>
        /// Abre el archivo para lectura con mmap.
        /// </summary>
        public void OpenRead()
        {
            if (_mmf != null)
                throw new InvalidOperationException("Database already open");

            if (!File.Exists(_filePath))
                throw new FileNotFoundException($"Database file not found: {_filePath}");

            _mmf = MemoryMappedFile.CreateFromFile(
                _filePath,
                FileMode.Open,
                null,
                0,
                MemoryMappedFileAccess.Read
            );

            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        }

        /// <summary>
        /// Abre el archivo para lectura/escritura con mmap.
        /// </summary>
        public void OpenReadWrite(long capacity = 0)
        {
            if (_mmf != null)
                throw new InvalidOperationException("Database already open");

            var fileMode = File.Exists(_filePath) ? FileMode.Open : FileMode.CreateNew;

            _mmf = MemoryMappedFile.CreateFromFile(
                _filePath,
                fileMode,
                null,
                capacity,
                MemoryMappedFileAccess.ReadWrite
            );

            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        }

        /// <summary>
        /// Lee una estructura en una posición específica.
        /// </summary>
        public T Read<T>(long offset) where T : struct
        {
            ThrowIfDisposed();
            
            T value;
            _accessor.Read(offset, out value);
            return value;
        }

        /// <summary>
        /// Escribe una estructura en una posición específica.
        /// </summary>
        public void Write<T>(long offset, T value) where T : struct
        {
            ThrowIfDisposed();
            _accessor.Write(offset, ref value);
        }

        /// <summary>
        /// Lee un array de bytes.
        /// </summary>
        public byte[] ReadBytes(long offset, int count)
        {
            ThrowIfDisposed();
            
            var buffer = new byte[count];
            _accessor.ReadArray(offset, buffer, 0, count);
            return buffer;
        }

        /// <summary>
        /// Escribe un array de bytes.
        /// </summary>
        public void WriteBytes(long offset, byte[] data)
        {
            ThrowIfDisposed();
            _accessor.WriteArray(offset, data, 0, data.Length);
        }

        /// <summary>
        /// Lee un string con longitud prefijada.
        /// </summary>
        public string ReadString(long offset)
        {
            ThrowIfDisposed();
            
            // Leer longitud (4 bytes)
            var length = _accessor.ReadInt32(offset);
            if (length <= 0 || length > 1024 * 1024) // Max 1MB
                return string.Empty;

            // Leer bytes del string
            var buffer = new byte[length];
            _accessor.ReadArray(offset + 4, buffer, 0, length);
            
            return System.Text.Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Escribe un string con longitud prefijada.
        /// </summary>
        public void WriteString(long offset, string value)
        {
            ThrowIfDisposed();
            
            var bytes = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
            
            // Escribir longitud
            _accessor.Write(offset, bytes.Length);
            
            // Escribir bytes
            _accessor.WriteArray(offset + 4, bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Fuerza el flush de datos al disco.
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();
            _accessor?.Flush();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _accessor?.Dispose();
            _mmf?.Dispose();
            
            _accessor = null;
            _mmf = null;
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MappedDatabase));
            
            if (_mmf == null)
                throw new InvalidOperationException("Database not open");
        }
    }

    /// <summary>
    /// Estructura de header para la base de datos.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DatabaseHeader
    {
        public uint Signature;      // 'SLSK' = 0x534C534B
        public ushort Version;      // Versión del formato
        public ushort Reserved;     // Reservado para futuro uso
        public long RecordCount;    // Número de registros
        public long IndexOffset;    // Offset del índice
        public long DataOffset;     // Offset de los datos

        public static readonly uint ValidSignature = 0x534C534B; // 'SLSK'
        public static readonly ushort CurrentVersion = 1;

        public bool IsValid => Signature == ValidSignature && Version == CurrentVersion;
    }
}
