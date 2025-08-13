using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace TataruLink.Utils;

public static class ImGuiUtils
{
    public static class Colors
    {
        // Basic colors
        public static readonly Vector4 White = new(1.0f, 1.0f, 1.0f, 1.0f);
        public static readonly Vector4 Black = new(0.0f, 0.0f, 0.0f, 1.0f);
        public static readonly Vector4 Transparent = new(0.0f, 0.0f, 0.0f, 0.0f);
        
        // Primary colors
        public static readonly Vector4 Red = new(1.0f, 0.0f, 0.0f, 1.0f);
        public static readonly Vector4 Green = new(0.0f, 1.0f, 0.0f, 1.0f);
        public static readonly Vector4 Blue = new(0.0f, 0.0f, 1.0f, 1.0f);
        public static readonly Vector4 Yellow = new(1.0f, 1.0f, 0.0f, 1.0f);
        public static readonly Vector4 Cyan = new(0.0f, 1.0f, 1.0f, 1.0f);
        public static readonly Vector4 Magenta = new(1.0f, 0.0f, 1.0f, 1.0f);
        
        // Soft colors
        public static readonly Vector4 Orange = new(1.0f, 0.5f, 0.0f, 1.0f);
        public static readonly Vector4 Pink = new(1.0f, 0.5f, 0.8f, 1.0f);
        public static readonly Vector4 Purple = new(0.7f, 0.0f, 1.0f, 1.0f);
        public static readonly Vector4 Peach = new(0.9f, 0.7f, 0.5f, 1.0f);
        
        // Light variants
        public static readonly Vector4 LightRed = new(1.0f, 0.4f, 0.4f, 1.0f);
        public static readonly Vector4 LightGreen = new(0.5f, 1.0f, 0.5f, 1.0f);
        public static readonly Vector4 LightBlue = new(0.4f, 0.8f, 1.0f, 1.0f);
        public static readonly Vector4 LightYellow = new(1.0f, 0.9f, 0.6f, 1.0f);
        public static readonly Vector4 LightCyan = new(0.5f, 1.0f, 0.8f, 1.0f);
        public static readonly Vector4 LightPurple = new(0.7f, 0.7f, 0.9f, 1.0f);
        public static readonly Vector4 LightGray = new(0.8f, 0.8f, 0.8f, 1.0f);
        
        // Pale variants  
        public static readonly Vector4 PaleGreen = new(0.8f, 1.0f, 0.5f, 1.0f);
        public static readonly Vector4 PaleBlue = new(0.8f, 0.8f, 1.0f, 1.0f);
        public static readonly Vector4 PaleYellow = new(0.9f, 0.9f, 0.6f, 1.0f);
        public static readonly Vector4 PaleRed = new(1.0f, 0.3f, 0.3f, 1.0f);
        
        // Dark variants
        public static readonly Vector4 DarkGray = new(0.3f, 0.3f, 0.3f, 1.0f);
        public static readonly Vector4 DarkBlue = new(0.0f, 0.0f, 0.5f, 1.0f);
        public static readonly Vector4 DarkGreen = new(0.0f, 0.5f, 0.0f, 1.0f);
        public static readonly Vector4 DarkRed = new(0.5f, 0.0f, 0.0f, 1.0f);
        
        // Gray scale
        public static readonly Vector4 Gray10 = new(0.1f, 0.1f, 0.1f, 1.0f);
        public static readonly Vector4 Gray20 = new(0.2f, 0.2f, 0.2f, 1.0f);
        public static readonly Vector4 Gray30 = new(0.3f, 0.3f, 0.3f, 1.0f);
        public static readonly Vector4 Gray40 = new(0.4f, 0.4f, 0.4f, 1.0f);
        public static readonly Vector4 Gray50 = new(0.5f, 0.5f, 0.5f, 1.0f);
        public static readonly Vector4 Gray60 = new(0.6f, 0.6f, 0.6f, 1.0f);
        public static readonly Vector4 Gray70 = new(0.7f, 0.7f, 0.7f, 1.0f);
        public static readonly Vector4 Gray80 = new(0.8f, 0.8f, 0.8f, 1.0f);
        public static readonly Vector4 Gray90 = new(0.9f, 0.9f, 0.9f, 1.0f);
        
        // UI semantic colors
        public static readonly Vector4 Error = new(1.0f, 0.3f, 0.3f, 1.0f);
        public static readonly Vector4 Warning = new(1.0f, 0.8f, 0.3f, 1.0f);
        public static readonly Vector4 Success = new(0.3f, 1.0f, 0.3f, 1.0f);
        public static readonly Vector4 Info = new(0.3f, 0.8f, 1.0f, 1.0f);
        
        // Text colors
        public static readonly Vector4 TextDefault = White;
        public static readonly Vector4 TextDisabled = Gray50;
        public static readonly Vector4 TextSubdued = Gray60;
        public static readonly Vector4 TextMuted = Gray70;
        
        // Background colors
        public static readonly Vector4 WindowBackground = new(0.06f, 0.06f, 0.06f, 1.0f);
        public static readonly Vector4 BorderDefault = new(0.43f, 0.43f, 0.50f, 0.50f);
        
        public static Vector4 WithAlpha(Vector4 color, float alpha)
        {
            return new Vector4(color.X, color.Y, color.Z, alpha);
        }
        
        public static Vector4 Darken(Vector4 color, float factor)
        {
            factor = Math.Clamp(factor, 0, 1);
            return new Vector4(
                color.X * (1 - factor),
                color.Y * (1 - factor),
                color.Z * (1 - factor),
                color.W
            );
        }
        
        public static Vector4 Lighten(Vector4 color, float factor)
        {
            factor = Math.Clamp(factor, 0, 1);
            return new Vector4(
                color.X + (1 - color.X) * factor,
                color.Y + (1 - color.Y) * factor,
                color.Z + (1 - color.Z) * factor,
                color.W
            );
        }
    }
    
    // Common UI helper methods
    public static void TextColored(Vector4 color, string text)
    {
        ImGui.TextColored(color, text);
    }
    
    public static void TextStatus(string label, bool isActive, string activeText = "Active", string inactiveText = "Inactive")
    {
        ImGui.Text($"{label}: ");
        ImGui.SameLine();
        ImGui.TextColored(isActive ? Colors.Success : Colors.TextDisabled, isActive ? activeText : inactiveText);
    }
    
    public static void TextWithTooltip(string text, string tooltip)
    {
        ImGui.Text(text);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
    }
    
    public static bool ButtonWithTooltip(string label, string tooltip)
    {
        var clicked = ImGui.Button(label);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
        return clicked;
    }
    
    public static void HelpMarker(string desc)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            using var tooltip = ImRaii.Tooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
            ImGui.TextUnformatted(desc);
            ImGui.PopTextWrapPos();
        }
    }
    
    public static void Section(string title)
    {
        ImGui.Separator();
        ImGui.TextColored(Colors.Info, title);
        ImGui.Separator();
    }
    
    public static void SectionSmall(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(Colors.TextMuted, title);
        ImGui.Separator();
    }
    
    public static bool InputTextWithHint(string label, string hint, ref string text, int maxLength = 100)
    {
        return ImGui.InputTextWithHint(label, hint, ref text, maxLength);
    }
    
    public static bool ColorEditWithReset(string label, ref Vector4 color, Vector4 defaultColor)
    {
        var changed = ImGui.ColorEdit4(label, ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);
        
        ImGui.SameLine();
        ImGui.PushID(label);
        if (ImGui.Button("Reset"))
        {
            color = defaultColor;
            changed = true;
        }
        ImGui.PopID();
        
        return changed;
    }
    
    public static void CenteredText(string text)
    {
        var windowWidth = ImGui.GetWindowSize().X;
        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
        ImGui.Text(text);
    }
    
    public static void Indent(Action content, float width = 0)
    {
        if (width > 0)
            ImGui.Indent(width);
        else
            ImGui.Indent();
            
        content();
        
        if (width > 0)
            ImGui.Unindent(width);
        else
            ImGui.Unindent();
    }
    
    public static bool ToggleButton(string label, ref bool value)
    {
        var activeColor = Colors.Success;
        var inactiveColor = Colors.Gray50;
        
        using (ImRaii.PushColor(ImGuiCol.Button, value ? activeColor : inactiveColor))
        {
            if (ImGui.Button(label))
            {
                value = !value;
                return true;
            }
        }
        return false;
    }
    
    public static void ProgressBar(float fraction, string label = "", Vector2? size = null)
    {
        var actualSize = size ?? new Vector2(-1, 0);
        ImGui.ProgressBar(fraction, actualSize, label);
    }
    
    public static bool BeginGroupBox(string label)
    {
        ImGui.BeginGroup();
        ImGui.TextColored(Colors.Info, label);
        ImGui.Separator();
        return true;
    }
    
    public static void EndGroupBox()
    {
        ImGui.Separator();
        ImGui.EndGroup();
    }
    
    public static void Spacing(int count = 1)
    {
        for (var i = 0; i < count; i++)
            ImGui.Spacing();
    }
    
    public static void AlignedLabel(string label, float alignment = 100f)
    {
        ImGui.Text(label);
        ImGui.SameLine(alignment);
    }
    
    public static bool ConfirmationButton(string label, string confirmText = "Are you sure?")
    {
        var id = $"##{label}_confirm";
        
        if (ImGui.Button(label))
        {
            ImGui.OpenPopup(id);
        }
        
        var confirmed = false;
        if (ImGui.BeginPopupModal(id, ref confirmed, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text(confirmText);
            ImGui.Separator();
            
            if (ImGui.Button("Yes", new Vector2(120, 0)))
            {
                confirmed = true;
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.SameLine();
            if (ImGui.Button("No", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }
        
        return confirmed;
    }
}
