using System.Collections.Generic;
using System.Windows;
using Frosty.Controls;

namespace BundleCompiler.Windows;

public partial class CompileBundleWindow : FrostyDockableWindow
{
    public string? Result = null;
    
    public CompileBundleWindow(List<string> options)
    {
        InitializeComponent();

        foreach (string option in options)
        {
            ComboBox.Items.Add(option);
        }
    }

    /// <summary>
    /// Show a property edit prompt. Do note, the object's properties will be edited directly, regardless of the users actions.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="title">The title of the window</param>
    /// <returns>Confirmation whether the user wants to cancel or proceed</returns>
    public static string? Show(List<string> options, string title = "Select Value")
    {
        CompileBundleWindow editPromptWindow = new CompileBundleWindow(options)
        {
            Title = title
        };
        editPromptWindow.ShowDialog();
            
        return editPromptWindow.Result;
    }

    public static string? Show(List<string> options, ResizeMode resizeMode, string title = "Select Value")
    {
        CompileBundleWindow editPromptWindow = new CompileBundleWindow(options)
        {
            Title = title,
            ResizeMode = resizeMode
        };
        editPromptWindow.ShowDialog();
            
        return editPromptWindow.Result;
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        Result = ComboBox.SelectedItem.ToString();
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}