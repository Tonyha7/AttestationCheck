using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

class Program
{
    static async Task Main(string[] args)
    {
        string inputPath;
        if (args.Length != 1)
        {
            Console.WriteLine("Enter File/Dir Path:");
            inputPath = Console.ReadLine();
        }
        else
        {
            inputPath = args[0];
        }

        if (Directory.Exists(inputPath))
        {
            await CheckCertificatesInFolderAsync(inputPath);
        }
        else if (File.Exists(inputPath))
        {
            await CheckCertificatesAsync(inputPath);
        }
        else
        {
            Console.WriteLine("Error! Input path is invalid.");
        }
        
        Console.WriteLine("Done.");
    }

    static async Task CheckCertificatesInFolderAsync(string folderPath)
    {
        foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.xml", SearchOption.AllDirectories))
        {
            Console.WriteLine("\n==============================");
            Console.WriteLine($"Checking file: {filePath}");
            await CheckCertificatesAsync(filePath);
        }
    }

    static async Task CheckCertificatesAsync(string filePath)
    {
        try
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetStringAsync("https://android.googleapis.com/attestation/status");
                var crl = System.Text.Json.JsonDocument.Parse(response).RootElement.GetProperty("entries");

                var certs = XDocument.Load(filePath)
                                     .Descendants("Certificate")
                                     .Select(elem => elem.Value)
                                     .ToList();

                string ecCertSn = ParseCert(certs[0]);
                string rsaCertSn = ParseCert(certs[3]);

                Console.WriteLine($"\nEC Serial: {ecCertSn}\nRSA Serial: {rsaCertSn}");

                if (crl.TryGetProperty(ecCertSn, out _) || crl.TryGetProperty(rsaCertSn, out _))
                {
                    Console.WriteLine("keybox.xml was banned!\n");
                    RenameFile(filePath, "Ban_");
                }
                else
                {
                    Console.WriteLine("keybox.xml works well!\n");
                    if (Path.GetFileName(filePath).StartsWith("Ban_"))
                    {
                        RenameFile(filePath, string.Empty, true);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error when processing {filePath} : {e.Message}");
        }
    }

    static string ParseCert(string cert)
    {
        cert = string.Join("\n", cert.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()));
        var parsedCert = new X509Certificate2(System.Text.Encoding.ASCII.GetBytes(cert));
        return parsedCert.SerialNumber.ToLower();
    }

    static void RenameFile(string filePath, string prefix, bool removePrefix = false)
    {
        string fileName = Path.GetFileName(filePath);
        string newFileName = removePrefix ? fileName.Substring(4) : prefix + fileName;
        string newFilePath = Path.Combine(Path.GetDirectoryName(filePath), newFileName);
        File.Move(filePath, newFilePath);
        Console.WriteLine($"Rename {filePath} -> {newFilePath}");
    }
}