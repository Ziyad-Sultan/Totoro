using ReactiveMarbles.ObservableEvents;
using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.UserControls;

namespace Totoro.WinUI.Dialogs.Views;

public class SubmitTimeStampsViewBase : ReactiveContentDialog<SubmitTimeStampsViewModel> { }

public sealed partial class SubmitTimeStampsView : SubmitTimeStampsViewBase
{
    public SubmitTimeStampsView()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.MediaPlayer)
                .Do(wrapper => MediaPlayerElement.SetMediaPlayer(wrapper.GetMediaPlayer()))
                .Subscribe()
                .DisposeWith(d);

            SetStartButton
            .Events()
            .Click
            .Select(_ => Unit.Default)
            .InvokeCommand(ViewModel.SetStartPosition)
            .DisposeWith(d);

            SetEndButton
            .Events()
            .Click
            .Select(_ => Unit.Default)
            .InvokeCommand(ViewModel.SetEndPosition)
            .DisposeWith(d);
        });
    }

    private void SubmitTimeStampsViewBase_CloseButtonClick(Microsoft.UI.Xaml.Controls.ContentDialog sender, Microsoft.UI.Xaml.Controls.ContentDialogButtonClickEventArgs args)
    {
        ViewModel.HandleClose = false;
    }

    private void SubmitTimeStampsViewBase_Closing(Microsoft.UI.Xaml.Controls.ContentDialog sender, Microsoft.UI.Xaml.Controls.ContentDialogClosingEventArgs args)
    {
        if (ViewModel.HandleClose)
        {
            args.Cancel = true;
        }
        else
        {
            ViewModel.MediaPlayer.Pause();
        }
    }
}
