using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using SeerNote.Domain;

namespace SeerNote.Agent
{
    [DataContract]
    public sealed class AgentNotePayload
    {
        public const string ContractName = "seernote.note.v1";

        [DataMember(Name = "schema", Order = 1)]
        public string Schema { get; set; }

        [DataMember(Name = "id", Order = 2)]
        public string Id { get; set; }

        [DataMember(Name = "title", Order = 3)]
        public string Title { get; set; }

        [DataMember(Name = "displayTitle", Order = 4)]
        public string DisplayTitle { get; set; }

        [DataMember(Name = "body", Order = 5)]
        public string Body { get; set; }

        [DataMember(Name = "category", Order = 6)]
        public string Category { get; set; }

        [DataMember(Name = "favorite", Order = 7)]
        public bool IsFavorite { get; set; }

        [DataMember(Name = "deleted", Order = 8)]
        public bool IsDeleted { get; set; }

        [DataMember(Name = "createdUtc", Order = 9)]
        public string CreatedUtc { get; set; }

        [DataMember(Name = "updatedUtc", Order = 10)]
        public string UpdatedUtc { get; set; }

        [DataMember(Name = "deletedUtc", Order = 11, EmitDefaultValue = false)]
        public string DeletedUtc { get; set; }

        public static AgentNotePayload FromEntry(Entry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return new AgentNotePayload
            {
                Schema = ContractName,
                Id = entry.Id.ToString("D").ToLowerInvariant(),
                Title = entry.Title ?? String.Empty,
                DisplayTitle = entry.DisplayTitle ?? String.Empty,
                Body = entry.Body ?? String.Empty,
                Category = entry.Category ?? String.Empty,
                IsFavorite = entry.IsFavorite,
                IsDeleted = entry.IsDeleted,
                CreatedUtc = Utc(entry.CreatedUtc),
                UpdatedUtc = Utc(entry.UpdatedUtc),
                DeletedUtc = entry.DeletedUtc.HasValue ? Utc(entry.DeletedUtc.Value) : null
            };
        }

        public static string Serialize(Entry entry)
        {
            return AgentJson.Serialize(FromEntry(entry));
        }

        private static string Utc(DateTime value)
        {
            return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }
    }

    public static class AgentJson
    {
        public static string Serialize<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(T),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
