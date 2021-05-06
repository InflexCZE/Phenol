using System.Collections;
using MahApps.Metro.Controls;
using NetPrints.Core;
using System.Windows;
using NetPrintsEditor.Utils;

namespace NetPrintsEditor.Dialogs
{
    /// <summary>
    /// Interaction logic for SelectTypeDialog.xaml
    /// </summary>
    public partial class SearchableComboboxDialog : MetroWindow
    {
        public static readonly DependencyProperty SelectedObjectProperty = DependencyProperty.Register(
            nameof(SelectedItem), typeof(object), typeof(SearchableComboboxDialog));

        public object SelectedItem
        {
            get => GetValue(SelectedObjectProperty);
            set => SetValue(SelectedObjectProperty, value);
        }

        public SearchableComboboxDialog(string header, IEnumerable items, object preSelected = null)
        {
            InitializeComponent();

            this.Title = header;
            this.SelectionBox.ItemsSource = items;

            if(preSelected is not null)
            {
                this.SelectedItem = preSelected;
            }
            
            this.SelectionBox.MakeComboBoxSearchable();
        }

        private void OnSelectButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
