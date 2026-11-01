using SmartRental.Models;

namespace SmartRental.ViewModels;

public class PhongtroDetailsViewModel
{
    public required Phongtro Phongtro { get; init; }

    public IReadOnlyList<NearbyLandmarkViewModel> NearbyLandmarks { get; init; }
        = Array.Empty<NearbyLandmarkViewModel>();
}

public class NearbyLandmarkViewModel
{
    public int Id { get; init; }

    public required string Ten { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public double DistanceKm { get; init; }
}
