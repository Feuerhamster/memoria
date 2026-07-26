using MessagePack;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.Utils;

public static class TrustedDataProvider {
	/// <summary>
	/// Generate a signed hash of a string
	/// </summary>
	/// <param name="data">data to hash and sign</param>
	/// <param name="secret"></param>
	/// <returns>signed hash as hex string</returns>
	public static byte[] GenerateSignedHash(byte[] data, byte[] secret) {
		using var hmac = new HMACSHA256(secret);
		
		var hashBytes = hmac.ComputeHash(data);
		return hashBytes;
	}
	
	public static string SignAndFormatData<T>(T data, byte[] secret) {
		var builder = new StringBuilder();
		
		var binData = MessagePackSerializer.Serialize(data, MessagePack.Resolvers.ContractlessStandardResolver.Options);
		
		var signature = GenerateSignedHash(binData, secret);
		
		builder.Append(Convert.ToBase64String(binData));
		var sig = Convert.ToHexString(signature);
		builder.Append(sig);
		
		return builder.ToString();
	}

	public static Result<T> DecodeAndVerifyData<T>(string raw, byte[] secret) where T : class {
		byte[] hash;
		byte[] rawData;

		var hashLength = 64;

		try {
			hash = Convert.FromHexString(raw.Substring(raw.Length - hashLength, hashLength));
			rawData = Convert.FromBase64String(raw.Substring(0, raw.Length - hashLength));
		}
		catch (Exception e) {
			return Result<T>.Failure(e);
		}

		var signature = GenerateSignedHash(rawData, secret);

		if (!hash.SequenceEqual(signature)) {
			return Result<T>.Failure("Hash verification failed");
		}

		var data = MessagePackSerializer.Deserialize<T>(rawData, MessagePack.Resolvers.ContractlessStandardResolver.Options);

		return Result<T>.Success(data);
	}
}