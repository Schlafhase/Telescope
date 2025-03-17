namespace Telescope_GUI;

public struct AppSettings
{
	public string? AccountEndpoint { get; set; } = null;
	public string? AccountKey { get; set; } = null;
	public string? SelectedDatabase { get; set; } = null;
	public string? SelectedContainer { get; set; } = null;
	
	public string LastQuery { get; set; } = "SELECT * FROM c";

	public int? PageSize { get; set; } = null;

	public AppSettings()
	{
		
	}
}