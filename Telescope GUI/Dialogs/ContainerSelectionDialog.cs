using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Terminal.Gui;

namespace Telescope_GUI;

public partial class  MainView
{
	private Dialog? _containerSelectionDialog;
	
	private async Task openContainerSelection()
	{
		_containerSelectionDialog = new Dialog("Select Container")
		{
			Width = Dim.Percent(25),
			Height = Dim.Percent(50)
		};

		Button cancelButton = new Button("Cancel");
		cancelButton.Clicked += () => _containerSelectionDialog.RequestStop();
		_containerSelectionDialog.AddButton(cancelButton);

		ListView listView;
		
		_containerSelectionDialog.Text = "Loading Containers...";
		
		_containerSelectionDialog.Initialized += async (_, _) =>
		{
			{
				try
				{
					List<ContainerProperties> containers = await _cosmosApiWrapper.ListContainers();
					
					listView = new ListView(containers.Select(d => d.Id).ToList())
					{
						X = 0,
						Y = 0,
						Width = Dim.Fill(),
						Height = Dim.Fill(1)
					};

					listView.OpenSelectedItem += async (e) =>
					{
						_appSettings.SelectedContainer = e.Value.ToString();
						_cosmosApiWrapper.SelectContainer(e.Value.ToString());
						updateTitle();
						updateQueryField();
						await File.WriteAllTextAsync("appsettings.json", JsonSerializer.Serialize(_appSettings));
						_containerSelectionDialog.RequestStop();
					};
					
					Button selectButton = new Button("Select");
					selectButton.Clicked += () => listView.OnOpenSelectedItem();

					_containerSelectionDialog.Text = "";
					_containerSelectionDialog.Add(listView);
					_containerSelectionDialog.AddButton(selectButton);
				}
				catch (InvalidOperationException e)
				{
					MessageBox.ErrorQuery("Error", e.Message, "Ok");
					_containerSelectionDialog.RequestStop();
				}
				catch (CosmosException e)
				{
					MessageBox.ErrorQuery("CosmosException",
										  "A CosmosException was thrown.\n" +
										  e.Message, "Ok");
					_containerSelectionDialog.RequestStop();
				}
			}
		};
		
		Application.Run(_containerSelectionDialog);
	}
}