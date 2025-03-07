﻿using NetPrints.Core;
using System.Windows;
using System.Windows.Controls;

namespace NetPrintsEditor.Controls
{
    public delegate void VariableGetSetDelegate(VariableGetSetControl sender, object callbackData, bool wasSet);

    /// <summary>
    /// Interaction logic for VariableGetSetControl.xaml
    /// </summary>
    public partial class VariableGetSetControl : UserControl
    {
        public static DependencyProperty CanGetProperty = DependencyProperty.Register(
            nameof(CanGet), typeof(bool), typeof(VariableGetSetControl));

        public static DependencyProperty CanSetProperty = DependencyProperty.Register(
            nameof(CanSet), typeof(bool), typeof(VariableGetSetControl));

        public event VariableGetSetDelegate OnVariableGetSet;

        public object CallbackData { get; set; }

        public bool CanGet
        {
            get => (bool)GetValue(CanGetProperty);
            set => SetValue(CanGetProperty, value);
        }

        public bool CanSet
        {
            get => (bool)GetValue(CanSetProperty);
            set => SetValue(CanSetProperty, value);
        }

        public VariableGetSetControl()
        {
            InitializeComponent();
        }

        private void OnVariableSetClicked(object sender, RoutedEventArgs e)
        {
            OnVariableGetSet?.Invoke(this, this.CallbackData, true);
        }

        private void OnVariableGetClicked(object sender, RoutedEventArgs e)
        {
            OnVariableGetSet?.Invoke(this, this.CallbackData, false);
        }

        public void ShowOrSelect()
        {
            var get = this.CanGet;
            var set = this.CanSet;

            if(get != set)
            {
                if(get)
                {
                    OnVariableGetClicked(this, null);
                }
                else
                {
                    OnVariableSetClicked(this, null);
                }
            }
            else
            {
                this.Visibility = Visibility.Visible;
            }
        }
    }
}
