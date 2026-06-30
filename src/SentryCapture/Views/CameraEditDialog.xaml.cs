using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using SentryCapture.Models;

namespace SentryCapture.Views;

public partial class CameraEditDialog : Window
{
    /// <summary>The resulting camera config (valid only when ShowDialog returns true).</summary>
    public CameraConfig Result { get; private set; } = new();

    private readonly string? _editingId;

    /// <summary>Add mode.</summary>
    public CameraEditDialog()
    {
        InitializeComponent();
        HeaderText.Text = "Add Camera";
        NameBox.Focus();
    }

    /// <summary>Edit mode — pre-fills the existing values.</summary>
    public CameraEditDialog(CameraConfig existing) : this()
    {
        HeaderText.Text = "Edit Camera";
        Title = "Edit Camera";
        _editingId = existing.Id;
        NameBox.Text = existing.Name;
        UrlBox.Text = existing.Url;
        HeadersBox.Text = HeadersToText(existing.Headers);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        string url = UrlBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Please enter a camera name.");
            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            ShowError("Please enter the image URL.");
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ShowError("The image URL must be a valid http:// or https:// address.");
            return;
        }

        if (!TryParseHeaders(HeadersBox.Text, out var headers, out string headerError))
        {
            ShowError(headerError);
            return;
        }

        Result = new CameraConfig
        {
            Id = _editingId ?? Guid.NewGuid().ToString("N"),
            Name = name,
            Url = url,
            Headers = headers
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static string HeadersToText(Dictionary<string, string> headers)
    {
        var sb = new StringBuilder();
        foreach (var kv in headers)
            sb.AppendLine($"{kv.Key}: {kv.Value}");
        return sb.ToString().TrimEnd();
    }

    private static bool TryParseHeaders(string text, out Dictionary<string, string> headers, out string error)
    {
        headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (string.IsNullOrWhiteSpace(text))
            return true;

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;

            int idx = line.IndexOf(':');
            if (idx <= 0 || idx == line.Length - 1)
            {
                error = $"Invalid header line: \"{line}\". Use the format \"Header: value\".";
                return false;
            }

            string key = line[..idx].Trim();
            string value = line[(idx + 1)..].Trim();
            if (key.Length == 0)
            {
                error = $"Invalid header name in line: \"{line}\".";
                return false;
            }

            headers[key] = value;
        }

        return true;
    }
}
