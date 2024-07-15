using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private static string RemoveCommentsAndEscapeNewlines(string json)
        {
            // Remove comments only outside of strings
            var pattern = @"(?<string>(""(?:[^""\\]|\\.)*""))|(?<comment>(\/\/.*?$|\/\*.*?\*\/))";
            var noCommentsJson = Regex.Replace(json, pattern, match =>
            {
                if (match.Groups["string"].Success)
                {
                    return match.Groups["string"].Value;
                }
                else
                {
                    return string.Empty;
                }
            }, RegexOptions.Multiline);

            // Escape newlines within JSON strings
            noCommentsJson = noCommentsJson.Replace("\n", "").Replace("\r", "");

            // Remove trailing commas
            pattern = @"(,)(\s*[\}\]])";
            noCommentsJson = Regex.Replace(noCommentsJson, pattern, "$2");

            return noCommentsJson;
        }

        private Dictionary<string, object> DeserializeJson(string json)
        {
            try
            {
                json = RemoveCommentsAndEscapeNewlines(json);
                return JsonSerializer.Deserialize<Dictionary<string, object>>(RemoveCommentsAndEscapeNewlines(json));
            }
            catch (JsonException ex)
            {
                // Handle JSON parsing error
                Console.WriteLine($"Error deserializing JSON: {ex.Message}");
                return null;
            }
        }
    }
}
