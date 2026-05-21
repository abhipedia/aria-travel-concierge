namespace TripPlannerV1.Services;

/// <summary>
/// A curated set of well-known cities / regions / landmarks per destination country.
/// Used to populate the "places you've already visited" picker for repeat visitors so we
/// can ask the LLM to skip those when planning their next trip.
/// </summary>
public interface IDestinationPlacesService
{
    /// <summary>Country name (case-insensitive) -> list of well-known places.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Map { get; }

    IReadOnlyList<string> GetPlaces(string? country);
}

public sealed class DestinationPlacesService : IDestinationPlacesService
{
    public DestinationPlacesService()
    {
        Map = Seed();
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Map { get; }

    public IReadOnlyList<string> GetPlaces(string? country) =>
        !string.IsNullOrWhiteSpace(country) && Map.TryGetValue(country.Trim(), out var list)
            ? list
            : Array.Empty<string>();

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Seed()
    {
        var d = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["India"] = new[] { "Delhi", "Agra (Taj Mahal)", "Jaipur", "Udaipur", "Jodhpur", "Mumbai", "Goa", "Kerala backwaters", "Varanasi", "Rishikesh", "Ladakh", "Darjeeling", "Hampi", "Andaman Islands" },
            ["Japan"] = new[] { "Tokyo", "Kyoto", "Osaka", "Nara", "Hiroshima", "Miyajima", "Hakone", "Mount Fuji area", "Nikko", "Kanazawa", "Takayama", "Shirakawa-go", "Sapporo", "Okinawa" },
            ["Italy"] = new[] { "Rome", "Florence", "Venice", "Milan", "Naples", "Amalfi Coast", "Cinque Terre", "Tuscany countryside", "Sicily", "Sardinia", "Lake Como", "Verona", "Bologna", "Pompeii" },
            ["France"] = new[] { "Paris", "Versailles", "Nice", "Cannes", "Monaco day trip", "Provence (Avignon, Aix)", "Lyon", "Bordeaux", "Loire Valley châteaux", "Mont Saint-Michel", "Normandy beaches", "Strasbourg", "Chamonix" },
            ["Spain"] = new[] { "Madrid", "Barcelona", "Seville", "Granada (Alhambra)", "Córdoba", "Toledo", "Valencia", "Bilbao", "San Sebastián", "Ibiza", "Mallorca", "Canary Islands" },
            ["Germany"] = new[] { "Berlin", "Munich", "Hamburg", "Frankfurt", "Cologne", "Dresden", "Heidelberg", "Rothenburg ob der Tauber", "Neuschwanstein Castle", "Black Forest", "Romantic Road" },
            ["United Kingdom"] = new[] { "London", "Edinburgh", "Bath", "Oxford", "Cambridge", "Stonehenge", "Cotswolds", "Lake District", "York", "Liverpool", "Cardiff", "Belfast", "Scottish Highlands" },
            ["United States"] = new[] { "New York City", "Washington D.C.", "Boston", "Miami", "New Orleans", "Chicago", "Las Vegas", "Los Angeles", "San Francisco", "Yosemite", "Grand Canyon", "Yellowstone", "Hawaii", "Seattle" },
            ["Canada"] = new[] { "Toronto", "Montreal", "Quebec City", "Ottawa", "Vancouver", "Victoria", "Banff", "Lake Louise", "Niagara Falls", "Whistler", "Jasper" },
            ["Mexico"] = new[] { "Mexico City", "Cancún", "Playa del Carmen", "Tulum", "Cozumel", "Oaxaca", "Puerto Vallarta", "San Miguel de Allende", "Mérida", "Chichén Itzá" },
            ["Brazil"] = new[] { "Rio de Janeiro", "São Paulo", "Salvador", "Iguazu Falls", "Manaus / Amazon", "Florianópolis", "Pantanal", "Fernando de Noronha" },
            ["Argentina"] = new[] { "Buenos Aires", "Mendoza wine country", "Bariloche", "Iguazu Falls", "El Calafate (Perito Moreno)", "Ushuaia", "Salta" },
            ["Peru"] = new[] { "Lima", "Cusco", "Sacred Valley", "Machu Picchu", "Rainbow Mountain", "Lake Titicaca", "Arequipa", "Amazon (Iquitos)" },
            ["Chile"] = new[] { "Santiago", "Valparaíso", "Atacama Desert", "Easter Island", "Patagonia (Torres del Paine)", "Pucón" },
            ["Australia"] = new[] { "Sydney", "Melbourne", "Brisbane", "Cairns / Great Barrier Reef", "Whitsundays", "Uluru", "Perth", "Tasmania", "Gold Coast", "Byron Bay" },
            ["New Zealand"] = new[] { "Auckland", "Wellington", "Rotorua", "Queenstown", "Milford Sound", "Christchurch", "Bay of Islands", "Tongariro" },
            ["Thailand"] = new[] { "Bangkok", "Chiang Mai", "Chiang Rai", "Phuket", "Krabi", "Koh Phi Phi", "Koh Samui", "Koh Phangan", "Koh Lanta", "Ayutthaya", "Sukhothai" },
            ["Vietnam"] = new[] { "Hanoi", "Halong Bay", "Sapa", "Hue", "Hoi An", "Da Nang", "Ho Chi Minh City", "Mekong Delta", "Phu Quoc" },
            ["Indonesia"] = new[] { "Bali (Ubud)", "Bali (Seminyak / Canggu)", "Jakarta", "Yogyakarta (Borobudur, Prambanan)", "Komodo Islands", "Lombok", "Gili Islands", "Raja Ampat" },
            ["Singapore"] = new[] { "Marina Bay", "Sentosa", "Gardens by the Bay", "Chinatown", "Little India", "Orchard Road", "Pulau Ubin" },
            ["Malaysia"] = new[] { "Kuala Lumpur", "Penang (George Town)", "Langkawi", "Malacca", "Borneo (Kota Kinabalu)", "Cameron Highlands" },
            ["Philippines"] = new[] { "Manila", "Cebu", "Bohol", "Palawan (El Nido)", "Palawan (Coron)", "Boracay", "Banaue rice terraces", "Siargao" },
            ["China"] = new[] { "Beijing", "Shanghai", "Xi'an (Terracotta Army)", "Guilin / Yangshuo", "Chengdu", "Hangzhou", "Suzhou", "Hong Kong", "Macau", "Zhangjiajie" },
            ["South Korea"] = new[] { "Seoul", "Busan", "Jeju Island", "Gyeongju", "Sokcho / Seoraksan", "DMZ" },
            ["United Arab Emirates"] = new[] { "Dubai", "Abu Dhabi", "Sharjah", "Al Ain", "Fujairah", "Ras Al Khaimah" },
            ["Turkey"] = new[] { "Istanbul", "Cappadocia", "Pamukkale", "Antalya", "Bodrum", "Ephesus", "Izmir" },
            ["Greece"] = new[] { "Athens", "Santorini", "Mykonos", "Crete", "Rhodes", "Corfu", "Meteora", "Delphi" },
            ["Egypt"] = new[] { "Cairo (Giza pyramids)", "Luxor", "Aswan", "Nile cruise", "Sharm El Sheikh", "Hurghada", "Alexandria" },
            ["Morocco"] = new[] { "Marrakech", "Fes", "Casablanca", "Rabat", "Chefchaouen", "Sahara (Merzouga)", "Essaouira", "Atlas Mountains" },
            ["South Africa"] = new[] { "Cape Town", "Johannesburg", "Kruger National Park", "Garden Route", "Stellenbosch wine country", "Drakensberg", "Durban" },
            ["Switzerland"] = new[] { "Zurich", "Geneva", "Lucerne", "Interlaken", "Jungfrau region", "Zermatt (Matterhorn)", "Bern", "Lausanne", "St. Moritz" },
            ["Netherlands"] = new[] { "Amsterdam", "Rotterdam", "The Hague", "Utrecht", "Keukenhof", "Giethoorn", "Maastricht" },
            ["Portugal"] = new[] { "Lisbon", "Porto", "Sintra", "Lagos / Algarve", "Madeira", "Azores", "Évora" },
            ["Iceland"] = new[] { "Reykjavik", "Golden Circle", "Blue Lagoon", "South Coast (Vík)", "Jökulsárlón glacier lagoon", "Westfjords", "Akureyri" },
            ["Ireland"] = new[] { "Dublin", "Galway", "Cork", "Cliffs of Moher", "Ring of Kerry", "Belfast (NI)", "Aran Islands" },
        };

        return d;
    }
}
