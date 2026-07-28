using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using FocusPanel.ViewModels;

namespace FocusPanel.Views;

public partial class AIAssistantView : UserControl
{
    private AIAssistantViewModel? _subscribedViewModel;

    public AIAssistantView()
    {
        InitializeComponent();
        ApiKeyBox.PasswordChanged += OnApiKeyChanged;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        DetachMessages();
        if (e.NewValue is AIAssistantViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            viewModel.Messages.CollectionChanged +=
                OnMessagesChanged;
        }
    }

    private void OnApiKeyChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is AIAssistantViewModel viewModel)
            viewModel.ApiKeyInput = ApiKeyBox.Password;
    }

    private void OnMessagesChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.BeginInvoke(
                new System.Action(
                    () => ConversationScroll.ScrollToEnd()));
        }
    }

    private void CredentialAction_Click(
        object sender,
        RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            new System.Action(
                () =>
                {
                    if (DataContext is AIAssistantViewModel viewModel
                        && string.IsNullOrEmpty(
                            viewModel.ApiKeyInput))
                    {
                        ApiKeyBox.Clear();
                    }
                }));
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        DetachMessages();
    }

    private void DetachMessages()
    {
        if (_subscribedViewModel == null)
            return;
        _subscribedViewModel.Messages.CollectionChanged -=
            OnMessagesChanged;
        _subscribedViewModel = null;
    }
}
