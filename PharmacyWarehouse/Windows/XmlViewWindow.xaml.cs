using System.Windows;

namespace PharmacyWarehouse.Windows;

public partial class XmlViewWindow : Window
{
    public XmlViewWindow(string requestXml, string? responseXml)
    {
        InitializeComponent();
        string content = $"Request XML:\n{requestXml}";
        if (!string.IsNullOrEmpty(responseXml))
        {
            content += $"\n\nResponse XML:\n{responseXml}";
        }
        txtXml.Text = content;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
