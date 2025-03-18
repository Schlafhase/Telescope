using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Terminal.Gui;

namespace Telescope_GUI;

public partial class MainView
{
	private Dialog? _databaseSelectionDialog;
	
	private async Task openDatabaseSelection()
	{
		_databaseSelectionDialog = new Dialog("Select Database")
		{
			Width = Dim.Percent(50),
			Height = Dim.Percent(50)
		};

		Button cancelButton = new Button("Cancel");
		cancelButton.Clicked += () => _databaseSelectionDialog.RequestStop();
		_databaseSelectionDialog.AddButton(cancelButton);

		// TODO: Use ListView
		try
		{
			List<DatabaseProperties> databases = await _cosmosApiWrapper.ListDatabases();

			foreach (DatabaseProperties database in databases)
			{
				Button button = new Button(database.Id)
				{
					Width = Dim.Fill()
				};
				button.Clicked += () =>
				{
					_appSettings.SelectedDatabase = database.Id;
					_cosmosApiWrapper.SelectDatabase(database.Id);
					File.WriteAllText("appsettings.json", JsonSerializer.Serialize(_appSettings));
					updateTitle();
					_databaseSelectionDialog.RequestStop();
				};

				_databaseSelectionDialog.Add(button);
			}
		}
		catch (InvalidOperationException e)
		{
			_databaseSelectionDialog.Text = e.Message;
		}

		Application.Run(_databaseSelectionDialog);
	}
}