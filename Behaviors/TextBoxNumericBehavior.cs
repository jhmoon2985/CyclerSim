using Microsoft.Xaml.Behaviors;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace CyclerSim.Behaviors
{
    public class TextBoxNumericBehavior : Behavior<TextBox>
    {
        public int DecimalPlaces { get; set; } = 2;
        public bool AllowNegative { get; set; } = false;
        public double MinValue { get; set; } = double.MinValue;
        public double MaxValue { get; set; } = double.MaxValue;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewTextInput += OnPreviewTextInput;
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
            AssociatedObject.LostFocus += OnLostFocus;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
            AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
            AssociatedObject.LostFocus -= OnLostFocus;
        }

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            if (textBox.SelectionLength > 0)
            {
                fullText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
                fullText = fullText.Insert(textBox.SelectionStart, e.Text);
            }

            e.Handled = !IsValidInput(fullText);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Allow control keys
            if (e.Key == Key.Delete || e.Key == Key.Back || e.Key == Key.Tab ||
                e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
            {
                return;
            }

            // Allow Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.A || e.Key == Key.C || e.Key == Key.V || e.Key == Key.X)
                {
                    return;
                }
            }
        }

        private void OnLostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (double.TryParse(textBox.Text, out double value))
            {
                // Clamp value to min/max
                value = Math.Max(MinValue, Math.Min(MaxValue, value));

                // Format to specified decimal places
                textBox.Text = value.ToString($"F{DecimalPlaces}", CultureInfo.InvariantCulture);
            }
            else if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "0" + (DecimalPlaces > 0 ? "." + new string('0', DecimalPlaces) : "");
            }
        }

        private bool IsValidInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return true;

            // Check for negative sign
            if (!AllowNegative && input.Contains("-"))
                return false;

            // Build regex pattern
            string pattern;
            if (DecimalPlaces > 0)
            {
                pattern = AllowNegative ? @"^-?\d*\.?\d{0," + DecimalPlaces + "}$" : @"^\d*\.?\d{0," + DecimalPlaces + "}$";
            }
            else
            {
                pattern = AllowNegative ? @"^-?\d*$" : @"^\d*$";
            }

            if (!Regex.IsMatch(input, pattern))
                return false;

            // Check if it's a valid number
            if (double.TryParse(input, out double value))
            {
                return value >= MinValue && value <= MaxValue;
            }

            // Allow partial input (like "3." or "-")
            return true;
        }
    }
}