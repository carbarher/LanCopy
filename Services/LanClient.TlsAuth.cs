using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using LanCopy.Models;

namespace LanCopy.Services;

public sealed partial class LanClient
{
    private async Task<(TcpClient tcp, Stream stream)> OpenAsync(CancellationToken ct)
    {
        var adaptiveBuffer = Math.Clamp(GetAdaptiveSocketBufferForHost(_host), 128 * 1024, 2 * 1024 * 1024);
        var tcp = new TcpClient
        {
            NoDelay = true,
            ReceiveBufferSize = adaptiveBuffer,
            SendBufferSize = adaptiveBuffer,
        };
        try
        {
        await tcp.ConnectAsync(_host, _port, ct);
        ConfigureSocket(tcp.Client);
        Stream stream = tcp.GetStream();

        // Feature 9: envolver con SslStream si TLS activo
        if (UseTls)
        {
            // TOFU real: fija la huella del cert del host en el primer uso y la verifica despues.
            CertTrust.ValidationResult trustResult = CertTrust.ValidationResult.InvalidCertificate;
            var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
            using var localCertificate = FileServer.LoadLocalCertificate()
                ?? throw new InvalidOperationException("st.certRejected");
            try
            {
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = _host,
                    ClientCertificates = new X509CertificateCollection { localCertificate },
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    RemoteCertificateValidationCallback = (s, c, ch, e) =>
                    {
                        if (c is X509Certificate2 c2) RemoteCertificate = c2;
                        else if (c != null) RemoteCertificate = new X509Certificate2(c);
                        trustResult = CertTrust.ValidateOrPinDetailed(_host, c);
                        return trustResult is CertTrust.ValidationResult.TrustedKnown
                            or CertTrust.ValidationResult.TrustedFirstUse;
                    },
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                }, ct);
                stream = ssl;
            }
            catch (AuthenticationException ex) when (trustResult == CertTrust.ValidationResult.IdentityChanged)
            {
                ssl.Dispose();
                throw new InvalidOperationException("st.identityChanged", ex);
            }
            catch (AuthenticationException ex) when (trustResult == CertTrust.ValidationResult.InvalidCertificate)
            {
                ssl.Dispose();
                throw new InvalidOperationException("st.certRejected", ex);
            }
            catch (System.IO.IOException)
            {
                if (!AllowPlaintextFallback)
                {
                    ssl.Dispose();
                    throw new InvalidOperationException("st.tlsPeerMismatch");
                }

                // El servidor cerró la conexión durante el handshake TLS (servidor sin TLS o en
                // texto plano). Reconectar sin TLS SOLO en modo de compatibilidad explícito.
                TlsFallbackOccurred?.Invoke(this, _host);
                ssl.Dispose();
                tcp.Dispose();
                tcp = new TcpClient
                {
                    NoDelay = true,
                    ReceiveBufferSize = adaptiveBuffer,
                    SendBufferSize = adaptiveBuffer,
                };
                await tcp.ConnectAsync(_host, _port, ct);
                ConfigureSocket(tcp.Client);
                stream = tcp.GetStream();
            }
        }

        // Lectura de cabeceras con buffer (sin consumir el payload binario que sigue).
        stream = new BufferedLineStream(stream);

        // Feature 10: enviar auth si PIN configurado
        await AuthenticateWithPinAsync(stream, ct);

        return (tcp, stream);
        }
        catch
        {
            // Si falla el handshake TLS o el auth PIN, no dejar el socket colgado.
            try { tcp.Dispose(); }
            catch (Exception ex)
            {
                Log.Warn("client", "open-cleanup-dispose-failed", new { host = _host, port = _port, error = ex.Message });
            }
            throw;
        }
    }

    private static void ConfigureSocket(Socket socket)
    {
        socket.NoDelay = true;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, KeepAliveIdleSeconds);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, KeepAliveIntervalSeconds);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, KeepAliveRetryCount);
        }
        catch (SocketException ex) { Log.Debug("client", "tcp-keepalive-tuning-socket-failed", new { error = ex.Message }); }
        catch (PlatformNotSupportedException ex) { Log.Debug("client", "tcp-keepalive-tuning-unsupported", new { error = ex.Message }); }
    }
}
