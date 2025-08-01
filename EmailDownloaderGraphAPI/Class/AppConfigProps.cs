﻿using System;
using System.Text.Json.Serialization;

namespace EmailGraphAPI.Classes {

    // Props odpovídající config.json
    internal class AppConfigProps {
        [JsonPropertyName("TenantId")]
        public string TenantId { get; set; }
        [JsonPropertyName("ClientId")]
        public string ClientId { get; set; }
        [JsonPropertyName("ClientSecret")]
        public string ClientSecret { get; set; }
        [JsonPropertyName("Mailbox")]
        public string Mailbox { get; set; }
        [JsonPropertyName("AllowedMailBoxes")]
        public List<string> AllowedMailBoxes { get; set; }
        [JsonPropertyName("DownloadPath")]
        public string DownloadPath { get; set; }
        [JsonPropertyName("StartDate")]
        public DateTime StartDate { get; set; }
        [JsonPropertyName("EmailPageSize")]
        public int EmailPageSize { get; set; }
    }
}
