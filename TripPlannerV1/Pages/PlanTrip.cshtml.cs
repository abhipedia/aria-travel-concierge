using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TripPlannerV1.Services;

namespace TripPlannerV1.Pages;

public class PlanTripModel : PageModel
{
    private readonly ITripPromptBuilder _promptBuilder;
    private readonly IAiClient _aiClient;
    private readonly ICountryCurrencyService _countries;
    private readonly IDestinationPlacesService _places;

    public PlanTripModel(
        ITripPromptBuilder promptBuilder,
        IAiClient aiClient,
        ICountryCurrencyService countries,
        IDestinationPlacesService places)
    {
        _promptBuilder = promptBuilder;
        _aiClient = aiClient;
        _countries = countries;
        _places = places;
    }

    [BindProperty]
    public TripInput Input { get; set; } = new();

    public string? ResultMessage { get; set; }
    public string? GeneratedPrompt { get; set; }
    public string? AiResponse { get; set; }
    public string? AiError { get; set; }
    public string? AiProvider { get; set; }
    public string? AiModel { get; set; }

    public IReadOnlyList<string> Countries => _countries.Countries;
    public IReadOnlyDictionary<string, string> CountryCurrencyMap => _countries.Map;
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DestinationPlacesMap => _places.Map;

    // Available travel types. Users may select one or more (mix and match).
    public static readonly IReadOnlyList<string> AvailableTravelTypes = new[]
    {
        "Foodie", "Adventure", "Artistic", "Cultural", "Historical",
        "Nature", "Beach", "Nightlife", "Shopping", "Relaxation",
        "Family", "Romantic", "Business", "Wellness", "Sports",
        "Wildlife & safari", "Photography", "Spiritual / pilgrimage",
        "Road trip", "Architecture & design", "Festivals & events",
        "Eco / sustainable", "LGBTQ+ friendly", "Backpacking", "Luxury",
        "Stargazing / astro",
    };

    public static readonly IReadOnlyList<string> AvailableDietaryPreferences = new[]
    {
        "Vegetarian", "Vegan", "Pescatarian", "Halal", "Kosher", "Jain",
        "Gluten-free", "Dairy-free", "Nut allergy", "Shellfish allergy",
        "Egg-free", "Soy allergy", "Sesame allergy",
        "Low-carb / keto", "Diabetic-friendly",
        "No pork", "No beef", "No alcohol",
    };

    // Common activities/cuisines travelers ask to avoid. Free-form also supported via "Other".
    public static readonly IReadOnlyList<string> AvailableAvoidInterests = new[]
    {
        "Hiking", "Long walks", "Heights", "Flying (small planes / helicopters)",
        "Boats / cruises", "Water sports", "Extreme sports", "Theme parks",
        "Museums", "Religious sites", "Nightlife / bars", "Shopping",
        "Gambling / casinos", "Seafood", "Spicy food", "Street food",
        "Alcohol", "Smoking areas", "Crowded tourist traps",
        "Red-eye / overnight flights", "Overnight trains or buses",
        "Self-driving abroad", "Cold weather", "Hot / humid weather",
        "High altitude", "Long road journeys",
        "Mosquito-heavy areas", "Group tours", "Early mornings",
    };

    public static readonly IReadOnlyList<string> TravelMonths = new[]
    {
        "Flexible", "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    public static readonly IReadOnlyList<string> AccommodationTiers = new[]
    {
        "Budget / Hostel", "Mid-range (3-star)", "Premium (4-star)",
        "Luxury (5-star)", "Boutique", "Airbnb / Apartment",
    };

    public static readonly IReadOnlyList<string> PaceOptions = new[]
    {
        "Packed", "Balanced", "Relaxed",
    };

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Cross-field: destination must differ from origin.
        if (!string.IsNullOrWhiteSpace(Input.OriginCountry) &&
            string.Equals(Input.OriginCountry?.Trim(), Input.DestinationCountry?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Input) + "." + nameof(TripInput.DestinationCountry),
                "Destination country must be different from the origin country.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var currency = _countries.GetCurrency(Input.OriginCountry) ?? "your local currency";

        // Combine the checkbox-selected visited places with any free-text additions
        // (comma-separated). Only honored when the traveler is NOT a first-time visitor.
        var visitedPlaces = new List<string>();
        if (!Input.IsFirstVisit)
        {
            if (Input.VisitedPlaces is { Count: > 0 })
            {
                visitedPlaces.AddRange(Input.VisitedPlaces.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
            }
            if (!string.IsNullOrWhiteSpace(Input.VisitedPlacesOther))
            {
                visitedPlaces.AddRange(Input.VisitedPlacesOther
                    .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            visitedPlaces = visitedPlaces
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Mirror logic for first-time visitors who pick must-see places they want to include.
        var wantedPlaces = new List<string>();
        if (Input.IsFirstVisit)
        {
            if (Input.WantedPlaces is { Count: > 0 })
            {
                wantedPlaces.AddRange(Input.WantedPlaces.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
            }
            if (!string.IsNullOrWhiteSpace(Input.WantedPlacesOther))
            {
                wantedPlaces.AddRange(Input.WantedPlacesOther
                    .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            wantedPlaces = wantedPlaces
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var travelTypes = Input.TravelTypes is { Count: > 0 }
            ? string.Join(", ", Input.TravelTypes)
            : "general";

        ResultMessage = $"Planning a {Input.Days}-day {travelTypes} trip for {Input.Travelers} traveler(s) from {Input.OriginCountry} to {Input.DestinationCountry} in {Input.TravelMonth} with a budget of {Input.Budget:N2} {currency} ({(Input.IsFirstVisit ? "first-time visitor" : "repeat visitor")}).";

        GeneratedPrompt = _promptBuilder.BuildPrompt(new TripPromptRequest(
            OriginCountry: Input.OriginCountry!,
            DestinationCountry: Input.DestinationCountry!,
            Budget: Input.Budget,
            Currency: currency,
            Days: Input.Days,
            TravelTypes: Input.TravelTypes,
            IsFirstVisit: Input.IsFirstVisit,
            Travelers: Input.Travelers,
            TravelMonth: Input.TravelMonth ?? "Flexible",
            AccommodationTier: Input.AccommodationTier ?? "Mid-range (3-star)",
            Pace: Input.Pace ?? "Balanced",
            DietaryPreferences: Input.DietaryPreferences,
            AvoidInterests: Input.AvoidInterests,
            VisitedPlaces: visitedPlaces,
            WantedPlaces: wantedPlaces));

        var result = await _aiClient.CompleteAsync(GeneratedPrompt, cancellationToken);
        AiProvider = result.Provider;
        AiModel = result.Model;
        if (result.Success)
        {
            AiResponse = result.Content;
        }
        else
        {
            AiError = result.Error;
        }

        return Page();
    }

    public class TripInput
    {
        [Required, Display(Name = "Origin country")]
        public string? OriginCountry { get; set; }

        [Required, Display(Name = "Destination country")]
        public string? DestinationCountry { get; set; }

        [Required, Range(1, 50, ErrorMessage = "Number of travelers must be between 1 and 50.")]
        [Display(Name = "Number of travelers")]
        public int Travelers { get; set; } = 1;

        [Required, Range(1, double.MaxValue, ErrorMessage = "Budget must be greater than zero.")]
        [Display(Name = "Total budget")]
        public decimal Budget { get; set; }

        [Required, Range(1, 365, ErrorMessage = "Number of days must be between 1 and 365.")]
        [Display(Name = "Number of days")]
        public int Days { get; set; }

        [Required, Display(Name = "Travel month")]
        public string? TravelMonth { get; set; } = "Flexible";

        [Required, Display(Name = "Accommodation preference")]
        public string? AccommodationTier { get; set; } = "Mid-range (3-star)";

        [Required, Display(Name = "Preferred pace")]
        public string? Pace { get; set; } = "Balanced";

        [Required(ErrorMessage = "Select at least one travel type.")]
        [MinLength(1, ErrorMessage = "Select at least one travel type.")]
        [Display(Name = "Travel type")]
        public List<string> TravelTypes { get; set; } = new();

        [Display(Name = "Dietary preferences / restrictions")]
        public List<string> DietaryPreferences { get; set; } = new();

        [Display(Name = "Interests to avoid")]
        public List<string> AvoidInterests { get; set; } = new();

        [Display(Name = "First time visiting the destination country?")]
        public bool IsFirstVisit { get; set; } = true;

        [Display(Name = "Places you've already visited")]
        public List<string> VisitedPlaces { get; set; } = new();

        [Display(Name = "Other places you've visited (comma-separated)")]
        public string? VisitedPlacesOther { get; set; }

        [Display(Name = "Must-sees you don't want to miss")]
        public List<string> WantedPlaces { get; set; } = new();

        [Display(Name = "Other places you'd like to include (comma-separated)")]
        public string? WantedPlacesOther { get; set; }
    }
}
