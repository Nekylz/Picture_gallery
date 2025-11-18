namespace PictureGallery.Views;

public partial class MyMainPage : ContentPage
{
	public MyMainPage()
	{
		InitializeComponent();
	}

	void OnPage1Clicked(object sender, EventArgs e)
	{
		// Fix: Use a container (e.g., ContentView) to host the ContentPage
		SubPage.Content = new ContentView { Content = new Views.NewPage1().Content };
	}
}