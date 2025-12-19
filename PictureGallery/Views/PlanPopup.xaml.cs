using CommunityToolkit.Maui.Views;

namespace PictureGallery.Views;

public partial class PlanPopup : Popup
{
    public PlanPopup()
    {
        InitializeComponent();
    }

    // Upgrade knop
    private void Upgrade_Clicked(object sender, EventArgs e)
    {
        PasswordEntry.IsVisible = true;
        ConfirmUpgradeButton.IsVisible = true;
    }

    // Bevestigen knop
    private void ConfirmUpgrade_Clicked(object sender, EventArgs e)
    {
        Close(PasswordEntry.Text);
    }
    
    // Enter toets in wachtwoord veld
    private void PasswordEntry_Completed(object sender, EventArgs e)
    {
        Close(PasswordEntry.Text);
    }

    // Popup sluiten
    private void Close_Clicked(object sender, EventArgs e)
    {
        Close(null);
    }
}