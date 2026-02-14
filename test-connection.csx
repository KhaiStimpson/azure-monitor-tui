using Azure.Storage.Queues;

// Test Azurite connection
var connectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;";

Console.WriteLine("Testing Azurite connection...");
Console.WriteLine($"Endpoint: http://127.0.0.1:10001");
Console.WriteLine();

try
{
    var client = new QueueServiceClient(connectionString);
    
    Console.WriteLine("Attempting to list queues...");
    var queues = client.GetQueues().ToList();
    
    Console.WriteLine($"✓ Success! Found {queues.Count} queue(s)");
    
    if (queues.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("No queues found. Create a test queue:");
        Console.WriteLine("  az storage queue create --name test-queue \\");
        Console.WriteLine("    --connection-string \"" + connectionString + "\"");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine("Queues:");
        foreach (var queue in queues)
        {
            Console.WriteLine($"  - {queue.Name}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Connection failed!");
    Console.WriteLine($"Error: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    
    if (ex.InnerException is not null)
    {
        Console.WriteLine($"Inner: {ex.InnerException.Message}");
    }
    
    Console.WriteLine();
    Console.WriteLine("Make sure Azurite is running:");
    Console.WriteLine("  docker run -p 10001:10001 mcr.microsoft.com/azure-storage/azurite");
}
