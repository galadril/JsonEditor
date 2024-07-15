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

            loadButton.Click += async (sender, e) =>
            {
                var viewModel = (MainWindowViewModel)DataContext;
                await viewModel.LoadJson(this);
                UpdateJsonEditorPanel(viewModel.JsonData);
                UpdateJsonRawEditor(viewModel.JsonData);
            };

            saveButton.Click += async (sender, e) =>
            {
                await SaveJson();
            };

            var tabControl = this.FindControl<TabControl>("TabControl");
            tabControl.SelectionChanged += TabControl_SelectionChanged;
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var jsonObject = ProcessPanel(JsonEditorPanel);
            var jsonString = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
            JsonRawEditor.Text = jsonString;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            JsonRawEditor = this.FindControl<TextEditor>("JsonRawEditor");
            JsonEditorPanel = this.FindControl<StackPanel>("JsonEditorPanel");
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
            foreach (Control child in panel.Children)
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
            foreach (Control itemControl in panel.Children)
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
            JsonValueKind valueKind = JsonValueKind.Undefined;
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
                JsonValueKind itemKind = templateElement.GetArrayLength() > 0 ? templateElement[0].ValueKind : JsonValueKind.String;
                object newItem = GetDefaultValueForKind(itemKind);
                var newItemControl = GenerateControlForValue(key, newItem, true);
                arrayPanel.Children.Insert(arrayPanel.Children.Count - 1, newItemControl);
            }
        }

        private object GetDefaultObjectFromTemplate(JsonElement templateObject)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonRepresentation = JsonSerializer.Serialize(templateObject, options);
            using (JsonDocument doc = JsonDocument.Parse(jsonRepresentation))
            {
                return doc.RootElement.Clone();
            }
        }

        private JsonElement GetDefaultValueForKind(JsonValueKind kind)
        {
            string jsonRepresentation = kind switch
            {
                JsonValueKind.String => "\"Text\"",
                JsonValueKind.Number => "0",
                JsonValueKind.Object => "{}",
                JsonValueKind.Array => "[]",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => "null"
            };

            using (JsonDocument doc = JsonDocument.Parse(jsonRepresentation))
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
