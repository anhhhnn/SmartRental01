using SmartRental.Models;

namespace SmartRental.ViewModels.Messages;

public sealed class MessagesIndexViewModel
{
    public string CurrentUserId { get; init; } = string.Empty;
    public IReadOnlyList<CuocTroChuyen> Conversations { get; init; } = Array.Empty<CuocTroChuyen>();
    public CuocTroChuyen? SelectedConversation { get; init; }
    public IReadOnlyList<TinNhan> Messages { get; init; } = Array.Empty<TinNhan>();
    public IReadOnlyList<LichXemPhong> Bookings { get; init; } = Array.Empty<LichXemPhong>();
    public bool IsCurrentUserOwner { get; init; }
}
