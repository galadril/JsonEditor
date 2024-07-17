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
using Avalonia.Input;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Highlighting;
using System.Reflection;

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

        private bool ValidateGeneratedControls()
        {
            var isValid = true; // Assume all controls are valid initially

            // Define a red border for indicating validation errors
            var errorBorderBrush = new SolidColorBrush(Colors.Red);
            var normalBorderBrush = new SolidColorBrush(Colors.Transparent); // Normal state

            // Iterate through each control in the JsonEditorPanel
            foreach (var control in JsonEditorPanel.Children)
            {
                if (control is StackPanel stackPanel && stackPanel.Children.Count > 1)
                {
                    var valueControl = stackPanel.Children[1];
                    var expectedType = (JsonValueKind)valueControl.Tag;
                    var controlIsValid = true; // Assume the current control is valid

                    switch (expectedType)
                    {
                        case JsonValueKind.String:
                            if (!(valueControl is TextBox)) controlIsValid = false;
                            break;
                        case JsonValueKind.Number:
                            if (valueControl is TextBox textBox)
                            {
                                // Check if the text can be parsed as a number
                                if (!double.TryParse(textBox.Text, out _)) controlIsValid = false;
                            }
                            else
                            {
                                controlIsValid = false;
                            }
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            if (!(valueControl is CheckBox)) controlIsValid = false;
                            break;
                        case JsonValueKind.Object:
                            // For objects, you might want to recursively validate nested controls, if applicable
                            if (!(valueControl is StackPanel)) controlIsValid = false;
                            break;
                        case JsonValueKind.Array:
                            // For arrays, you might also want to validate each item in the array
                            if (!(valueControl is StackPanel)) controlIsValid = false;
                            break;
                        default:
                            // Handle other types or unexpected cases
                            controlIsValid = false;
                            break;
                    }

                    // Update the border of the control based on its validation status
                    if (!controlIsValid)
                    {
                        if (valueControl is TextBox textBox)
                        {
                            textBox.BorderBrush = errorBorderBrush;
                            textBox.BorderThickness = new Thickness(2);
                        }
                        // Add similar cases for other control types if needed
                        isValid = false; // Mark the overall validation as failed
                    }
                    else
                    {
                        // Reset to normal state if the control is valid
                        if (valueControl is TextBox textBox)
                        {
                            textBox.BorderBrush = normalBorderBrush;
                            textBox.BorderThickness = new Thickness(0);
                        }
                        // Add similar reset cases for other control types if needed
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
                            control = new TextBox { Text = jsonElement.GetString(), Styles = { flatStyle } };
                            break;
                        case JsonValueKind.Number:
                            control = new TextBox { Text = jsonElement.GetRawText(), Styles = { flatStyle } };
                            break;
                        case JsonValueKind.Object:
                            var innerPanel = new StackPanel { Margin = new Thickness(10, 5, 0, 5) };
                            var innerDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                            GenerateUI(innerDict, innerPanel);
                            control = innerPanel;
                            break;
                        case JsonValueKind.Array:
                            arrayPanel = new StackPanel { Styles = { flatStyle } };
                            foreach (var item in jsonElement.EnumerateArray())
                            {
                                var itemControl = GenerateControlForValue(key, item, true);
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
                var addButton = new Button { Margin = new Thickness(5, 0, 0, 0) };
                var pathIcon = new PathIcon
                {
                    Data = Geometry.Parse("M12 7C12.4142 7 12.75 7.33579 12.75 7.75V11.25H16.25C16.6642 11.25 17 11.5858 17 12C17 12.4142 16.6642 12.75 16.25 12.75H12.75V16.25C12.75 16.6642 12.4142 17 12 17C11.5858 17 11.25 16.6642 11.25 16.25V12.75H7.75C7.33579 12.75 7 12.4142 7 12C7 11.5858 7.33579 11.25 7.75 11.25H11.25V7.75C11.25 7.33579 11.5858 7 12 7Z M3 6.25C3 4.45507 4.45507 3 6.25 3H17.75C19.5449 3 21 4.45507 21 6.25V17.75C21 19.5449 19.5449 21 17.75 21H6.25C4.45507 21 3 19.5449 3 17.75V6.25ZM6.25 4.5C5.2835 4.5 4.5 5.2835 4.5 6.25V17.75C4.5 18.7165 5.2835 19.5 6.25 19.5H17.75C18.7165 19.5 19.5 18.7165 19.5 17.75V6.25C19.5 5.2835 18.7165 4.5 17.75 4.5H6.25Z"),
                    Width = 16,
                    Height = 16
                };

                addButton.Content = pathIcon;
                addButton.Click += (sender, e) => AddArrayItem(key, arrayPanel, element);

                arrayPanel.Children.Add(addButton);
            }

            // Tag the control with the JsonValueKind to use later during saving
            control.Tag = valueKind;
            return control;
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
    }
}
