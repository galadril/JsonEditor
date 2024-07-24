using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using System;
using System.Diagnostics;
using Avalonia.Input;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Highlighting;
using System.Reflection;
using Avalonia.Layout;
using System.Linq;
using System.Text.RegularExpressions;

namespace JsonEditor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel
            {
                JsonEditorPanel = this.FindControl<StackPanel>("JsonEditorPanel")
            };

            var loadButton = this.FindControl<Button>("LoadJsonButton");
            var saveButton = this.FindControl<Button>("SaveJsonButton");

            this.FindControl<Button>("MinimizeButton").Click += (s, e) => this.WindowState = WindowState.Minimized;
            this.FindControl<Button>("MaximizeButton").Click += (s, e) =>
                this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            this.FindControl<Button>("CloseButton").Click += (s, e) => this.Close();
            this.FindControl<Border>("TitleBar").PointerPressed += TitleBar_PointerPressed;

            loadButton.Click += async (sender, e) =>
            {
                var viewModel = (MainWindowViewModel)DataContext;
                await viewModel.LoadJson(this);
                UpdateJsonEditorPanel(viewModel.JsonData);
                UpdateJsonRawEditor(viewModel.JsonData);
            };

            saveButton.Click += async (sender, e) =>
            {
                var tabControl = this.FindControl<TabControl>("TabControl");
                if (tabControl.SelectedIndex == 0)
                {
                    if (!ValidateGeneratedControls())
                    {
                        ShowErrorMessage("Validation failed. Please check the data types of all fields.");
                        return;
                    }
                }
                else if (tabControl.SelectedIndex == 1)
                {
                    if (!ValidateJsonStructure(JsonRawEditor.Text))
                    {
                        ShowErrorMessage("Invalid JSON structure. Please correct the JSON and try again.");
                        return;
                    }
                }

                await SaveJson();
            };

            var tabControl = this.FindControl<TabControl>("TabControl");
            tabControl.SelectionChanged += TabControl_SelectionChanged;
        }
        private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private bool isProgrammaticTabChange = false; // Flag to indicate programmatic tab changes

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isProgrammaticTabChange) return; // Bypass if change is programmatic

            var tabControl = sender as TabControl;
            if (tabControl == null) return;

            // Moving from tab 1 (Generated Controls) to tab 2 (Raw Editor)
            if (tabControl.SelectedIndex == 1)
            {
                // Validate all field values if they match the datatype
                if (!ValidateGeneratedControls())
                {
                    // If validation fails, show an error and switch back to the Generated Controls tab
                    ShowErrorMessage("Validation failed. Please check the data types of all fields.");

                    isProgrammaticTabChange = true; // Set the flag
                    tabControl.SelectedIndex = 0; // Switch back to the Generated Controls tab
                    isProgrammaticTabChange = false; // Reset the flag
                    return;
                }

                var jsonObject = ProcessPanel(JsonEditorPanel);
                var jsonString = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
                JsonRawEditor.Text = jsonString;
            }
            // Moving from tab 2 (Raw Editor) back to tab 1 (Generated Controls)
            else if (tabControl.SelectedIndex == 0)
            {
                // Validate the structure of the JSON
                if (!ValidateJsonStructure(JsonRawEditor.Text))
                {
                    // If validation fails, show an error and switch back to the Raw Editor tab
                    ShowErrorMessage("Invalid JSON structure. Please correct the JSON and try again.");

                    isProgrammaticTabChange = true; // Set the flag
                    tabControl.SelectedIndex = 1; // Switch back to the Generated Controls tab
                    isProgrammaticTabChange = false; // Reset the flag
                    return;
                }

                var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonRawEditor.Text);
                UpdateJsonEditorPanel(jsonData);
            }
        }
        
        private bool ValidateControl(Control control, JsonValueKind expectedType)
        {
            var isValid = true; // Assume the control is valid initially
            var errorBorderBrush = new SolidColorBrush(Colors.Red);
            var normalBorderBrush = new SolidColorBrush(Colors.Transparent); // Normal state

            switch (expectedType)
            {
                case JsonValueKind.String:
                    // Check if the control is a TextBox or a StackPanel containing a TextBox
                    if (!(control is TextBox) && !(control is StackPanel stackPanel && stackPanel.Children.Count > 0 && stackPanel.Children[0] is TextBox))
                    {
                        isValid = false;
                    }
                    break;
                case JsonValueKind.Number:
                    if (control is TextBox textBoxNumber)
                    {
                        // Check if the text can be parsed as a number
                        if (!double.TryParse(textBoxNumber.Text, out _)) isValid = false;
                    }
                    else
                    {
                        isValid = false;
                    }
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (!(control is CheckBox)) isValid = false;
                    break;
                case JsonValueKind.Object:
                    if (control is StackPanel nestedPanel)
                    {
                        // Recursively validate each child control in the nested panel
                        foreach (var child in nestedPanel.Children)
                        {
                            if (child is StackPanel childPanel && childPanel.Children.Count > 1)
                            {
                                var valueControl = childPanel.Children[1] as Control;
                                if (valueControl != null && valueControl.Tag is JsonValueKind childExpectedType)
                                {
                                    isValid &= ValidateControl(valueControl, childExpectedType); // Use bitwise AND to accumulate validity
                                }
                            }
                        }
                    }
                    else
                    {
                        isValid = false;
                    }
                    break;
                case JsonValueKind.Array:
                    if (control is StackPanel arrayPanel)
                    {
                        // Recursively validate each child control in the array panel
                        foreach (var child in arrayPanel.Children)
                        {
                            if (child is Control arrayItemControl && arrayItemControl.Tag is JsonValueKind arrayItemExpectedType)
                            {
                                isValid &= ValidateControl(arrayItemControl, arrayItemExpectedType); // Use bitwise AND to accumulate validity
                            }
                        }
                    }
                    else
                    {
                        isValid = false;
                    }
                    break;
                case JsonValueKind.Undefined:
                case JsonValueKind.Null:
                default:
                    // Handle other types or unexpected cases
                    isValid = false;
                    break;
            }

            // Update the border of the control based on its validation status
            if (!isValid)
            {
                if (control is TextBox textBoxInvalid)
                {
                    textBoxInvalid.BorderBrush = errorBorderBrush;
                    textBoxInvalid.BorderThickness = new Thickness(2);
                }
                else if (control is StackPanel stackPanelInvalid && stackPanelInvalid.Children.Count > 0 && stackPanelInvalid.Children[0] is TextBox)
                {
                    // If the control is a StackPanel containing a TextBox, apply border to the TextBox
                    var textBoxInStack = stackPanelInvalid.Children[0] as TextBox;
                    textBoxInStack.BorderBrush = errorBorderBrush;
                    textBoxInStack.BorderThickness = new Thickness(2);
                }
            }
            else
            {
                // Reset to normal state if the control is valid
                if (control is TextBox textBoxValid)
                {
                    textBoxValid.BorderBrush = normalBorderBrush;
                    textBoxValid.BorderThickness = new Thickness(0);
                }
                else if (control is StackPanel stackPanelValid && stackPanelValid.Children.Count > 0 && stackPanelValid.Children[0] is TextBox)
                {
                    // If the control is a StackPanel containing a TextBox, reset border of the TextBox
                    var textBoxInStack = stackPanelValid.Children[0] as TextBox;
                    textBoxInStack.BorderBrush = normalBorderBrush;
                    textBoxInStack.BorderThickness = new Thickness(0);
                }
            }

            return isValid;
        }

        private bool ValidateGeneratedControls()
        {
            var isValid = true; // Assume all controls are valid initially

            // Iterate through each control in the JsonEditorPanel
            foreach (var control in JsonEditorPanel.Children)
            {
                if (control is StackPanel stackPanel && stackPanel.Children.Count > 1)
                {
                    var valueControl = stackPanel.Children[1] as Control;
                    if (valueControl != null && valueControl.Tag is JsonValueKind expectedType)
                    {
                        // Validate the control and accumulate the overall validity
                        isValid &= ValidateControl(valueControl, expectedType);
                    }
                }
            }

            return isValid; // Return the overall validation status
        }

        private bool ValidateJsonStructure(string jsonText)
        {
            try
            {
                JsonDocument.Parse(jsonText);
                return true; // Valid JSON structure
            }
            catch (JsonException)
            {
                return false; // Invalid JSON structure
            }
        }

        private async void ShowErrorMessage(string message)
        {
            var dialog = new Window
            {
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Title = "Error",
            };

            // Create a TextBlock for the message
            var messageTextBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(10) // Add some margin for better spacing
            };

            // Create an OK button to close the dialog
            var okButton = new Button
            {
                Content = "OK",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Thickness(10) // Add some margin for better spacing
            };
            okButton.Click += (_, __) => dialog.Close();

            // Use a StackPanel to layout the message and the Button
            var panel = new StackPanel
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            panel.Children.Add(messageTextBlock);
            panel.Children.Add(okButton);

            // Set the StackPanel as the dialog content
            dialog.Content = panel;

            // Show the dialog
            await dialog.ShowDialog(this); // 'this' should be your main window or current window context
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            JsonRawEditor = this.FindControl<TextEditor>("JsonRawEditor");
            JsonEditorPanel = this.FindControl<StackPanel>("JsonEditorPanel");

            JsonRawEditor.TextArea.TextView.LinkTextForegroundBrush = Brushes.White;
            JsonRawEditor.TextArea.TextView.LinkTextUnderline = false;

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "JsonEditor.Resources.Json.xshd";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            using var xmlReader = new System.Xml.XmlTextReader(reader);
            JsonRawEditor.SyntaxHighlighting = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
            
            // Create and attach the context menu
            var contextMenu = new ContextMenu();
            var cutMenuItem = new MenuItem { Header = "Cut" };
            cutMenuItem.Click += (sender, e) => ExecuteCommand(ApplicationCommands.Cut);
            var copyMenuItem = new MenuItem { Header = "Copy" };
            copyMenuItem.Click += (sender, e) => ExecuteCommand(ApplicationCommands.Copy);
            var pasteMenuItem = new MenuItem { Header = "Paste" };
            pasteMenuItem.Click += (sender, e) => ExecuteCommand(ApplicationCommands.Paste);

            contextMenu.Items.Add(cutMenuItem);
            contextMenu.Items.Add(copyMenuItem);
            contextMenu.Items.Add(pasteMenuItem);
            JsonRawEditor.ContextMenu = contextMenu;
        }
        private void ExecuteCommand(RoutedCommand command)
        {
            // Get the focused control
            if (command == ApplicationCommands.Cut)
            {
                JsonRawEditor.Cut();
            }
            else if (command == ApplicationCommands.Copy)
            {
                JsonRawEditor.Copy();
            }
            else if (command == ApplicationCommands.Paste)
            {
                JsonRawEditor.Paste();
            }
        }

        private async Task SaveJson()
        {
            var jsonObject = ProcessPanel(JsonEditorPanel);
            var jsonString = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
            var dialog = new SaveFileDialog();
            var result = await dialog.ShowAsync(this);
            if (result != null)
            {
                var path = result;
                await File.WriteAllTextAsync(path, jsonString);
            }
        }

        private object ProcessControl(Control control)
        {
            if (control.Tag is JsonValueKind valueKind)
            {
                switch (valueKind)
                {
                    case JsonValueKind.String:
                        if (control is TextBox textBox)
                        {
                            return textBox.Text;
                        }
                        else if (control is StackPanel stackPanel && stackPanel.Children.Count > 0 && stackPanel.Children[0] is TextBox textBoxInPanel)
                        {
                            return textBoxInPanel.Text;
                        }
                        break;
                    case JsonValueKind.Number:
                        if (control is TextBox textBox2)
                        {
                            if (double.TryParse(textBox2.Text, out var number))
                            {
                                return number;
                            }
                        }
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        if (control is CheckBox checkBox)
                        {
                            return checkBox.IsChecked == true;
                        }
                        break;
                    case JsonValueKind.Object:
                        if (control is StackPanel panel)
                        {
                            return ProcessPanel(panel);
                        }
                        break;
                    case JsonValueKind.Array:
                        if (control is StackPanel arrayPanel)
                        {
                            return ProcessArrayPanel(arrayPanel);
                        }
                        break;
                }
            }
            return null;
        }

        private Dictionary<string, object> ProcessPanel(Panel panel)
        {
            if (panel == null)
                return null;

            var result = new Dictionary<string, object>();
            foreach (var child in panel.Children)
            {
                if (child is StackPanel stackPanel && stackPanel.Children[0] is TextBlock keyTextBlock)
                {
                    var key = keyTextBlock.Text;
                    if (stackPanel.Children.Count > 1 && stackPanel.Children[1] is Control valueControl)
                    {
                        var value = ProcessControl(valueControl);
                        if (value != null)
                        {
                            result[key] = value;
                        }
                    }
                }
            }
            return result;
        }

        private List<object> ProcessArrayPanel(Panel panel)
        {
            var array = new List<object>();
            foreach (var itemControl in panel.Children)
            {
                var value = ProcessControl(itemControl);
                if (value != null)
                {
                    array.Add(value);
                }
            }
            return array;
        }

        private void UpdateJsonEditorPanel(Dictionary<string, object> jsonData)
        {
            JsonEditorPanel.Children.Clear();
            if (jsonData != null)
            {
                GenerateUI(jsonData, JsonEditorPanel);
            }
        }

        private void GenerateUI(Dictionary<string, object> jsonData, Panel parent)
        {
            foreach (var kvp in jsonData)
            {
                var stackPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(0, 5) };

                var textBlock = new TextBlock { Text = kvp.Key, Width = 100, Margin = new Thickness(0, 0, 10, 0) };
                stackPanel.Children.Add(textBlock);

                var valueControl = GenerateControlForValue(kvp.Key, kvp.Value);
                stackPanel.Children.Add(valueControl);

                parent.Children.Add(stackPanel);
            }
        }

             private void GenerateUI(Dictionary<string, object> jsonData, Panel parent, bool isSub = false)
        {
            foreach (var kvp in jsonData)
            {
                var stackPanel = new StackPanel
                {
                    Orientation = isSub ? Avalonia.Layout.Orientation.Vertical : Avalonia.Layout.Orientation.Horizontal,
                    Margin = new Thickness(0, 5)
                };

                var textBlock = new TextBlock
                {
                    Text = kvp.Key,
                    Margin = new Thickness(0, 5, 10, 0),
                    TextAlignment = TextAlignment.Start,
                    VerticalAlignment = isSub ? VerticalAlignment.Center : VerticalAlignment.Top
                };

                stackPanel.Children.Add(textBlock);

                var valueControl = GenerateControlForValue(kvp.Key, kvp.Value);
                stackPanel.Children.Add(valueControl);

                parent.Children.Add(stackPanel);
            }
        }

        private Control GenerateControlForValue(string key, object value, bool isArrayItem = false)
        {
            Control control = null;
            var valueKind = JsonValueKind.Undefined;
            StackPanel arrayPanel = null; // Declare outside to be accessible for the "Add" button logic

            // Common styling for flat UI
            var flatStyle = new Style(selector => selector.OfType<Control>());
            flatStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(2)));

            switch (value)
            {
                case JsonElement jsonElement:
                    valueKind = jsonElement.ValueKind;
                    switch (jsonElement.ValueKind)
                    {
                        case JsonValueKind.String:
                            string textValue = jsonElement.GetString();
                            control = new TextBox { Text = textValue, Styles = { flatStyle }};

                            // Check if the string value is a file path
                            if (IsFilePath(textValue))
                            {
                                var openFolderButton = new Button
                                {
                                    Margin = new Thickness(5, 0, 0, 0),
                                    HorizontalAlignment = HorizontalAlignment.Left
                                };
                                if (this.TryFindResource("icon_folder", out object iconGeometry) && iconGeometry is StreamGeometry geometry)
                                {
                                    var pathIcon = new PathIcon
                                    {
                                        Data = geometry,
                                        Width = 16,
                                        Height = 16
                                    };
                                    openFolderButton.Content = pathIcon;
                                }
                                openFolderButton.Click += (sender, e) => OpenExplorer(textValue);

                                var stackPanel = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    HorizontalAlignment = HorizontalAlignment.Left
                                };
                                stackPanel.Children.Add(control);
                                stackPanel.Children.Add(openFolderButton);

                                control = stackPanel;
                            }
                            else  if (IsUrl(textValue))
                            {
                                var openBrowserButton = new Button
                                {
                                    Margin = new Thickness(5, 0, 0, 0),
                                    HorizontalAlignment = HorizontalAlignment.Left
                                };
                                if (this.TryFindResource("icon_browser", out object iconGeometry) && iconGeometry is StreamGeometry geometry)
                                {
                                    var pathIcon = new PathIcon
                                    {
                                        Data = geometry,
                                        Width = 16,
                                        Height = 16
                                    };
                                    openBrowserButton.Content = pathIcon;
                                }

                                openBrowserButton.Click += (sender, e) => OpenBrowser(textValue);
                                var stackPanel = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    HorizontalAlignment = HorizontalAlignment.Left
                                };
                                stackPanel.Children.Add(control);
                                stackPanel.Children.Add(openBrowserButton);
                                control = stackPanel;
                            }
                            break;
                        case JsonValueKind.Number:
                            control = new TextBox { Text = jsonElement.GetRawText(), Styles = { flatStyle } };
                            break;
                        case JsonValueKind.Object:
                            var innerPanel = new StackPanel { Margin = new Thickness(10, 5, 0, 5) };
                            var innerDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                            GenerateUI(innerDict, innerPanel, true);
                            control = innerPanel;
                            break;
                        case JsonValueKind.Array:
                            arrayPanel = new StackPanel { Styles = { flatStyle } };
                            foreach (var itemControl in jsonElement.EnumerateArray().Select(item => GenerateControlForValue(key, item, true)))
                            {
                                arrayPanel.Children.Add(itemControl);
                            }
                            control = arrayPanel;
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            control = new CheckBox { IsChecked = jsonElement.GetBoolean(), Styles = { flatStyle } };
                            break;
                        case JsonValueKind.Null:
                            control = new TextBlock { Text = "null", Styles = { flatStyle } };
                            break;
                        default:
                            control = new TextBlock { Text = "Unsupported type", Styles = { flatStyle } };
                            break;
                    }
                    break;
                default:
                    control = new TextBlock { Text = value?.ToString() ?? "null", Styles = { flatStyle } };
                    break;
            }

            // Moved outside the switch statement
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Array && !isArrayItem && arrayPanel != null)
            {
                var addButton = new Button
                {
                    Margin = new Thickness(0, 0, 0, 0),
                    HorizontalContentAlignment = HorizontalAlignment.Center, // Center the content horizontally
                    HorizontalAlignment = HorizontalAlignment.Left // Prevent the button from stretching
                };

                if (this.TryFindResource("icon_add", out object iconGeometry) && iconGeometry is StreamGeometry geometry)
                {
                    var pathIcon = new PathIcon
                    {
                        Data = geometry,
                        Width = 16,
                        Height = 16
                    };
                    addButton.Content = pathIcon;
                }

                addButton.Click += (sender, e) => AddArrayItem(key, arrayPanel, element);
                arrayPanel.Children.Add(addButton);
            }

            // Tag the control with the JsonValueKind to use later during saving
            control.Tag = valueKind;
            return control;
        }

        private bool IsFilePath(string text)
        {
            string pattern = @"^(?:[a-zA-Z]:|[\\/]{2}[^\\/]+[\\/]+[^\\/]+)?[\\/]+[^\\/][^:*?""<>|/]+(?:\\[^:*?""<>|/]+)*\\?$";
            return !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text, pattern);
        }

        private bool IsUrl(string text)
        {
            string pattern = @"^(http|https):\/\/[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,}(:[0-9]+)?(\/.*)?$";
            return !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text, pattern);
        }


        private void AddArrayItem(string key, Panel arrayPanel, JsonElement templateElement)
        {
            // Check if the array contains objects and use the first item as a template
            if (templateElement.GetArrayLength() > 0 && templateElement[0].ValueKind == JsonValueKind.Object)
            {
                var templateObject = templateElement[0];
                var newItem = GetDefaultObjectFromTemplate(templateObject);
                var newItemControl = GenerateControlForValue(key, newItem, true);
                arrayPanel.Children.Insert(arrayPanel.Children.Count - 1, newItemControl);
            }
            else
            {
                // Fallback to the original behavior for simple types
                var itemKind = templateElement.GetArrayLength() > 0 ? templateElement[0].ValueKind : JsonValueKind.String;
                object newItem = GetDefaultValueForKind(itemKind);
                var newItemControl = GenerateControlForValue(key, newItem, true);
                arrayPanel.Children.Insert(arrayPanel.Children.Count - 1, newItemControl);
            }
        }

        private object GetDefaultObjectFromTemplate(JsonElement templateObject)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonRepresentation = JsonSerializer.Serialize(templateObject, options);
            using (var doc = JsonDocument.Parse(jsonRepresentation))
            {
                return doc.RootElement.Clone();
            }
        }

        private JsonElement GetDefaultValueForKind(JsonValueKind kind)
        {
            var jsonRepresentation = kind switch
            {
                JsonValueKind.String => "\"Text\"",
                JsonValueKind.Number => "0",
                JsonValueKind.Object => "{}",
                JsonValueKind.Array => "[]",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => "null"
            };

            using (var doc = JsonDocument.Parse(jsonRepresentation))
            {
                return doc.RootElement.Clone();
            }
        }

        private void UpdateJsonRawEditor(Dictionary<string, object> jsonData)
        {
            if (jsonData != null)
            {
                var jsonString = JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true });
                JsonRawEditor.Text = jsonString;
            }
            else
            {
                JsonRawEditor.Text = string.Empty;
            }
        }

        public void OpenExplorer(string path)
        {
            Task.Run(() =>
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = path
                    }
                };

                process.Start();
            });
        }

        public void OpenBrowser(string url)
        {
            Task.Run(() =>
            {
                try
                {
                    // Use the Process.Start method to open the URL in the default browser.
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true // This is important for .NET Core and .NET 5+ compatibility
                    });
                }
                catch (Exception ex)
                {}
            });
        }
    }
}
