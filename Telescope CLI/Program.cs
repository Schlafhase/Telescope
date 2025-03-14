using Microsoft.Azure.Cosmos;
using Telescope;

namespace Telescope_CLI;

internal abstract class Program
{
	private static readonly string _connectionString;
	
	private static async Task Main(string[] args)
	{
		CosmosClient client = new CosmosClient(_connectionString);
		CosmosApiWrapper cosmosApiWrapper = new(client);
		
		cosmosApiWrapper.SelectDatabase("cactus-messenger");
		cosmosApiWrapper.SelectContainer("cactus-messenger");
		
		await cosmosApiWrapper.GetFirstPageByQueryAsync("""
														SELECT * FROM c WHERE c.Type = "message" ORDER BY c.Content
														""");
		
		foreach (dynamic entity in cosmosApiWrapper._loadedEntities)
		{
			Console.WriteLine(entity);
		}
		
		Console.WriteLine("Done");

		await cosmosApiWrapper.LoadMore();
		
		foreach (dynamic entity in cosmosApiWrapper._loadedEntities)
		{
			Console.WriteLine(entity);
		}
	}
}