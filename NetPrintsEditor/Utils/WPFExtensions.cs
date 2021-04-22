using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NetPrintsEditor.Utils
{
    public static class WPFExtensions
    {
        public static void MakeComboBoxSearchable(this ComboBox targetComboBox)
        {
            targetComboBox.Loaded += TargetComboBox_Loaded;
        }

        private static void TargetComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var targetComboBox = sender as ComboBox;
            var targetTextBox = targetComboBox?.Template.FindName("PART_EditableTextBox", targetComboBox) as TextBox;

            if (targetTextBox == null) return;

            targetComboBox.Tag = "TextInput";
            targetComboBox.StaysOpenOnEdit = true;
            targetComboBox.IsEditable = true;
            targetComboBox.IsTextSearchEnabled = false;

            targetTextBox.TextChanged += (o, args) =>
            {
                var textBox = (TextBox)o;

                var searchText = textBox.Text;

                if (targetComboBox.Tag.ToString() == "Selection")
                {
                    targetComboBox.Tag = "TextInput";
                    targetComboBox.IsDropDownOpen = true;
                }
                else
                {
                    if (targetComboBox.SelectionBoxItem != null)
                    {
                        targetComboBox.SelectedItem = null;
                        targetTextBox.Text = searchText;
                        textBox.CaretIndex = int.MaxValue;
                    }

                    if (string.IsNullOrEmpty(searchText))
                    {
                        targetComboBox.Items.Filter = item => true;
                        targetComboBox.SelectedItem = default;
                    }
                    else
                    {
                        targetComboBox.Items.Filter = item =>
                        {
                            return item.ToString().IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) != -1;
                        };
                    }

                    Keyboard.ClearFocus();
                    Keyboard.Focus(targetTextBox);
                    targetComboBox.IsDropDownOpen = true;
                    targetTextBox.SelectionStart = targetTextBox.Text.Length;
                }
            };


            targetComboBox.SelectionChanged += (o, args) =>
            {
                var comboBox = o as ComboBox;
                if (comboBox?.SelectedItem == null) return;
                comboBox.Tag = "Selection";
            };
        }
    }
}
