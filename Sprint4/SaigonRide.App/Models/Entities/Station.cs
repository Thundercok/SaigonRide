namespace SaigonRide.App.Models.Entities;

public class Station
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Rental> StartRentals { get; set; } = new List<Rental>();
    public ICollection<Rental> EndRentals { get; set; } = new List<Rental>();
    public ICollection<StationUtilisationLog> UtilisationLogs { get; set; } = new List<StationUtilisationLog>();
}