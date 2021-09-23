using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using PgpCore;

namespace AzureFunctionsPGPEncrypt
{
    public static class PGPEncrypt
    {
        private const string PublicKeyEnvironmentVariable = "pgp-public-key";

        [FunctionName(nameof(PGPEncrypt))]
        public static async Task Run(
            [BlobTrigger("testingcontainer/{blobName}.{blobExtension}", Connection = "AzureWebJobsStorage")] Stream blobStream,
            [Blob("testingcontainerencrypted/{blobName}.pgp", FileAccess.Write)] Stream encryptedBlob,
            string blobName,
            string blobExtension,
            ILogger log            
        ) {
            log.LogInformation("Getting local variables");
            string publicKeyBase64 = Environment.GetEnvironmentVariable("pgp-public-key", EnvironmentVariableTarget.Process);
            byte[] publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            string publicKey = Encoding.UTF8.GetString(publicKeyBytes);

            log.LogInformation("Setting pgp content");
            Stream encryptedStream = await EncryptAsync(blobStream, publicKey);

            log.LogInformation("Writting to output trigger");
            using (MemoryStream memoryStream = new MemoryStream())
            {
                encryptedStream.CopyTo(memoryStream);
                var byteArray = memoryStream.ToArray();
                await encryptedBlob.WriteAsync(byteArray, 0, byteArray.Length);
            }
        }

        private static async Task<Stream> EncryptAsync(Stream inputStream, string publicKey)
        {
            using (PGP pgp = new PGP())
            {
                Stream outputStream = new MemoryStream();

                using (inputStream)
                using (Stream publicKeyStream = GenerateStreamFromString(publicKey))
                {
                    await pgp.EncryptStreamAsync(inputStream, outputStream, publicKeyStream, true, true);
                    outputStream.Seek(0, SeekOrigin.Begin);
                    return outputStream;
                }
            }
        }

        private static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
