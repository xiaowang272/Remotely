using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;

namespace Remotely.Desktop.UI.Services;

public class ChatUiService : IChatUiService
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IViewModelFactory _viewModelFactory;
    private IChatWindowViewModel? _chatViewModel;

    public ChatUiService(
        IUiDispatcher dispatcher,
        IDialogProvider dialogProvider,
        IViewModelFactory viewModelFactory)
    {
        _dispatcher = dispatcher;
        // dialogProvider kept for DI compatibility but unused in silent mode
        _viewModelFactory = viewModelFactory;
    }

    public event EventHandler? ChatWindowClosed;

    public async Task ReceiveChat(ChatMessage chatMessage)
    {
        await _dispatcher.InvokeAsync(() =>
        {
            if (chatMessage.Disconnected)
            {
                // Silent mode: exit without showing dialog
                Environment.Exit(0);
                return;
            }

            if (_chatViewModel != null)
            {
                _chatViewModel.SenderName = chatMessage.SenderName;
                _chatViewModel.ChatMessages.Add(chatMessage);
            }
        });
    }

    public void ShowChatWindow(string organizationName, StreamWriter writer)
    {
        // Silent mode: initialize chat ViewModel for pipe communication but do not show window
        _dispatcher.Post(() =>
        {
            _chatViewModel = _viewModelFactory.CreateChatWindowViewModel(organizationName, writer);
            // ChatWindow is not displayed in silent mode
            // ViewModel is initialized to maintain pipe communication functionality
        });
    }
}
