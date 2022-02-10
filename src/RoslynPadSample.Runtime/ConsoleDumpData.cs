namespace RoslynPadSample.Runtime
{
    using System;
    using System.Text.Json;

    public sealed class ConsoleDumpData
    {
        public string Type { get; set; }

        public string Content { get; set; }

        public ConsoleDumpData()
        {
        }

        private ConsoleDumpData(Type type, string content)
        {
            Type = type.FullName;
            Content = content;
        }

        public static ConsoleDumpData Create<T>(T data)
        {
            return new ConsoleDumpData(typeof(T), JsonSerializer.Serialize(data));
        }

        public static object DeserializeContent(string data)
        {
            var consoleDumpData = JsonSerializer.Deserialize<ConsoleDumpData>(data);
            if (consoleDumpData == null)
            {
                return null;
            }

            return JsonSerializer.Deserialize(consoleDumpData.Content, System.Type.GetType(consoleDumpData.Type));
        }
    }
}