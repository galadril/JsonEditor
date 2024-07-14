using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace JsonEditor
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Dictionary<string, object> _jsonData;
        public Dictionary<string, object> JsonData
        {
            get => _jsonData;
            set => this.RaiseAndSetIfChanged(ref _jsonData, value);
        }

        public ICommand LoadJsonCommand { get; }
        public ICommand SaveJsonCommand { get; }
        public StackPanel JsonEditorPanel { get; set; }

        public MainWindowViewModel()
        {
            JsonData = new Dictionary<string, object>();
            LoadJsonCommand = ReactiveCommand.CreateFromTask<Window>(LoadJson);
        }

        public async Task LoadJson(Window window)
        {
            var dialog = new OpenFileDialog();
            var result = await dialog.ShowAsync(window);

            if (result != null && result.Length > 0)
            {
                var path = result[0];
                var json = await File.ReadAllTextAsync(path);
                JsonData = DeserializeJson(json);
            }
        }

        private Dictionary<string, object> DeserializeJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch (JsonException ex)
            {
                // Handle JSON parsing error
                Console.WriteLine($"Error deserializing JSON: {ex.Message}");
                return null;
            }
        }

        private string SerializeJson(Dictionary<string, object> jsonData)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize(jsonData, options);
        }
    }
}
