using System.Text.Json;
using Terminal.Gui;

namespace Telescope_GUI;

public partial class MainView
{
	private Dialog? _credentialConfigurationDialog;
	
	private Task openCredentialConfiguration()
	{
		_credentialConfigurationDialog = new Dialog("Configure Credentials")
		{
			Width = Dim.Percent(50),
			Height = Dim.Percent(50)
		};

		Label accountEndpointLabel = new Label("Account Endpoint:")
		{
			X = 1,
			Y = 1
		};
		TextField accountEndpointField = new TextField(_appSettings.AccountEndpoint ?? "")
		{
			X = 1,
			Y = Pos.Bottom(accountEndpointLabel),
			Width = Dim.Fill()
		};

		Label accountKeyLabel = new Label("Account Key:")
		{
			X = 1,
			Y = Pos.Bottom(accountEndpointField) + 1
		};
		TextField accountKeyField = new TextField(_appSettings.AccountKey ?? "")
		{
			X = 1,
			Y = Pos.Bottom(accountKeyLabel),
			Width = Dim.Fill()
		};

		Button cancelButton = new Button("Cancel");
		cancelButton.Clicked += () => _credentialConfigurationDialog.RequestStop();

		Button saveButton = new Button("Save");
		saveButton.Clicked += async () =>
		{
			try
			{
				_cosmosApiWrapper.SetCredentials(accountEndpointField.Text.ToString(), accountKeyField.Text.ToString());
			}
			catch (ArgumentException e)
			{
				MessageBox.ErrorQuery("Error", e.ParamName + "is a required field.", "Ok");
				return;
			}
			catch (UriFormatException)
			{
				MessageBox.ErrorQuery("Error", "Invalid URI format.", "Ok");
				return;
			}

			if (!await _cosmosApiWrapper.VerifyConnection())
			{
				MessageBox.ErrorQuery("Error", "Invalid credentials.", "Ok");
				return;
			}

			_appSettings.AccountEndpoint = accountEndpointField.Text.ToString();
			_appSettings.AccountKey = accountKeyField.Text.ToString();
			await File.WriteAllTextAsync("appsettings.json", JsonSerializer.Serialize(_appSettings));

			_credentialConfigurationDialog.RequestStop();
		};

		_credentialConfigurationDialog.Add(accountEndpointLabel, accountEndpointField, accountKeyLabel,
										   accountKeyField);

		_credentialConfigurationDialog.AddButton(cancelButton);
		_credentialConfigurationDialog.AddButton(saveButton);

		Application.Run(_credentialConfigurationDialog);
		return Task.CompletedTask;
	}
}