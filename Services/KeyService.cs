using System.Security.Cryptography;
using System.Text;

namespace Memoria.Services;

public interface IKeyService {
	public string GenerateRandomSecureUniqueString(int length);
	public byte[] GenerateDerivedSecret(string name, int length);
	public byte[] GenerateGuid();
	public byte[] OidcTransferTokenKey { get; }
}

public class KeyService : IKeyService {
	private readonly byte[] _mainSecret;
	private readonly ILogger<KeyService> _logger;

	public byte[] OidcTransferTokenKey { get; }
	
	public KeyService(IConfiguration config, ILogger<KeyService> logger) {
		this._logger = logger;
		
		var secret = config.GetValue<string>("MainSecret");

		if (string.IsNullOrEmpty(secret)) {
			secret = this.GenerateRandomSecureUniqueString(32);
			logger.LogCritical("No main secret provided in config, using a randomly generated one now.");
		}
		
		this._mainSecret = Encoding.UTF8.GetBytes(secret);
		this.OidcTransferTokenKey = this.GenerateDerivedSecret("", 32);
	}

	/// <summary>
	/// Generate a derived secret from the main secret. Use this if you need a secret for anything.
	/// </summary>
	/// <param name="name">A name, info or descriptor for the secret (usually service name)</param>
	/// <param name="length">length of the secret</param>
	/// <returns></returns>
	public byte[] GenerateDerivedSecret(string name, int length) {
		var nameBytes = Encoding.UTF8.GetBytes(name);
		var salt = SHA256.HashData(nameBytes);
	
		this._logger.LogInformation($"Derived secret generated: {name}");
		
		return HKDF.DeriveKey(HashAlgorithmName.SHA256, this._mainSecret, length, salt, nameBytes);
	}

	/// <summary>
	/// Generate a random secure unique string of a given length as hexadecimal.
	/// </summary>
	/// <param name="length"></param>
	/// <returns>hexadecimal random bytes</returns>
	public string GenerateRandomSecureUniqueString(int length) {
		using var rng = RandomNumberGenerator.Create();
		var bytes = new byte[length];
		rng.GetBytes(bytes);
		return Convert.ToHexString(bytes);
	}

	/// <summary>
	/// Generates a new Guid as byte array
	/// </summary>
	/// <returns>Guid</returns>
	public byte[] GenerateGuid() {
		return Guid.NewGuid().ToByteArray();
	}
}