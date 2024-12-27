namespace Genelife.Domain.Housing;

public class Address
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }

    public Address(string street, string city, string postalCode)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
    }

    public override string ToString() => $"{Street}, {City} {PostalCode}";
}