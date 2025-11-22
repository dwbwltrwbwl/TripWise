using System.Text.Json.Serialization;

namespace TripWise.Models
{
    // Модель для старта поиска
    public class AviasalesSearchRequestV2
    {
        [JsonPropertyName("marker")]
        public string Marker { get; set; }

        [JsonPropertyName("locale")]
        public string Locale { get; set; } = "ru";

        [JsonPropertyName("currency_code")]
        public string CurrencyCode { get; set; } = "RUB";

        [JsonPropertyName("market_code")]
        public string MarketCode { get; set; } = "ru";

        [JsonPropertyName("search_params")]
        public SearchParams SearchParams { get; set; }
    }

    public class SearchParams
    {
        [JsonPropertyName("trip_class")]
        public string TripClass { get; set; } = "Y";

        [JsonPropertyName("passengers")]
        public Passengers Passengers { get; set; }

        [JsonPropertyName("directions")]
        public List<Direction> Directions { get; set; }
    }

    public class Passengers
    {
        [JsonPropertyName("adults")]
        public int Adults { get; set; } = 1;

        [JsonPropertyName("children")]
        public int Children { get; set; } = 0;

        [JsonPropertyName("infants")]
        public int Infants { get; set; } = 0;
    }

    public class Direction
    {
        [JsonPropertyName("origin")]
        public string Origin { get; set; }

        [JsonPropertyName("destination")]
        public string Destination { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }
    }

    // Ответ на старт поиска
    public class AviasalesSearchResponseV2
    {
        [JsonPropertyName("search_id")]
        public string SearchId { get; set; }

        [JsonPropertyName("results_url")]
        public string ResultsUrl { get; set; }
    }

    // Запрос результатов поиска
    public class AviasalesResultsRequest
    {
        [JsonPropertyName("search_id")]
        public string SearchId { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 50;

        [JsonPropertyName("last_update_timestamp")]
        public long LastUpdateTimestamp { get; set; } = 0;
    }

    // Ответ с результатами поиска
    public class AviasalesResultsResponse
    {
        [JsonPropertyName("tickets")]
        public List<Ticket> Tickets { get; set; }

        [JsonPropertyName("agents")]
        public Dictionary<string, Agent> Agents { get; set; }

        [JsonPropertyName("airlines")]
        public Dictionary<string, Airline> Airlines { get; set; }

        [JsonPropertyName("airports")]
        public Dictionary<string, Airport> Airports { get; set; }

        [JsonPropertyName("flight_legs")]
        public List<FlightLeg> FlightLegs { get; set; }

        [JsonPropertyName("search_params")]
        public SearchParamsResponse SearchParams { get; set; }

        [JsonPropertyName("last_update_timestamp")]
        public long LastUpdateTimestamp { get; set; }

        [JsonPropertyName("is_over")]
        public bool IsOver { get; set; }
    }

    public class Ticket
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("segments")]
        public List<Segment> Segments { get; set; }

        [JsonPropertyName("proposals")]
        public List<Proposal> Proposals { get; set; }

        [JsonPropertyName("signature")]
        public string Signature { get; set; }
    }

    public class Segment
    {
        [JsonPropertyName("flights")]
        public List<int> Flights { get; set; }

        [JsonPropertyName("transfers")]
        public List<Transfer> Transfers { get; set; }
    }

    public class Transfer
    {
        [JsonPropertyName("night_transfer")]
        public bool NightTransfer { get; set; }

        [JsonPropertyName("recheck_baggage")]
        public bool RecheckBaggage { get; set; }

        [JsonPropertyName("airport_change")]
        public bool AirportChange { get; set; }

        [JsonPropertyName("short_layover")]
        public bool ShortLayover { get; set; }

        [JsonPropertyName("visa_rules")]
        public VisaRules VisaRules { get; set; }
    }

    public class VisaRules
    {
        [JsonPropertyName("required")]
        public bool Required { get; set; }
    }

    public class Proposal
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("agent_id")]
        public string AgentId { get; set; }

        [JsonPropertyName("price")]
        public Price Price { get; set; }

        [JsonPropertyName("price_per_person")]
        public Price PricePerPerson { get; set; }

        [JsonPropertyName("flight_terms")]
        public FlightTerms FlightTerms { get; set; }
    }

    public class Price
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }
    }

    public class FlightTerms
    {
        [JsonPropertyName("trip_class")]
        public string TripClass { get; set; }

        [JsonPropertyName("seats_available")]
        public int? SeatsAvailable { get; set; }

        [JsonPropertyName("baggage")]
        public BaggageInfo Baggage { get; set; }

        [JsonPropertyName("handbags")]
        public BaggageInfo Handbags { get; set; }

        [JsonPropertyName("additional_tariff_info")]
        public AdditionalTariffInfo AdditionalTariffInfo { get; set; }
    }

    public class BaggageInfo
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("weight")]
        public int? Weight { get; set; }

        [JsonPropertyName("total_weight")]
        public int? TotalWeight { get; set; }
    }

    public class AdditionalTariffInfo
    {
        [JsonPropertyName("return_before_flight")]
        public bool? ReturnBeforeFlight { get; set; }

        [JsonPropertyName("return_after_flight")]
        public bool? ReturnAfterFlight { get; set; }

        [JsonPropertyName("change_before_flight")]
        public bool? ChangeBeforeFlight { get; set; }

        [JsonPropertyName("change_after_flight")]
        public bool? ChangeAfterFlight { get; set; }
    }

    public class FlightLeg
    {
        [JsonPropertyName("origin")]
        public string Origin { get; set; }

        [JsonPropertyName("destination")]
        public string Destination { get; set; }

        [JsonPropertyName("departure_unix_timestamp")]
        public long DepartureUnixTimestamp { get; set; }

        [JsonPropertyName("arrival_unix_timestamp")]
        public long ArrivalUnixTimestamp { get; set; }

        [JsonPropertyName("local_departure_date_time")]
        public string LocalDepartureDateTime { get; set; }

        [JsonPropertyName("local_arrival_date_time")]
        public string LocalArrivalDateTime { get; set; }

        [JsonPropertyName("operating_carrier_designator")]
        public string OperatingCarrierDesignator { get; set; }

        [JsonPropertyName("equipment")]
        public Equipment Equipment { get; set; }

        [JsonPropertyName("technical_stops")]
        public List<TechnicalStop> TechnicalStops { get; set; }
    }

    public class Equipment
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class TechnicalStop
    {
        [JsonPropertyName("airport")]
        public string Airport { get; set; }

        [JsonPropertyName("duration_seconds")]
        public int DurationSeconds { get; set; }
    }

    public class Agent
    {
        [JsonPropertyName("gate_name")]
        public string GateName { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("payment_methods")]
        public List<string> PaymentMethods { get; set; }
    }

    public class Airline
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("is_lowcost")]
        public bool IsLowcost { get; set; }
    }

    public class Airport
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }
    }

    public class SearchParamsResponse
    {
        [JsonPropertyName("passengers")]
        public Passengers Passengers { get; set; }

        [JsonPropertyName("trip_class")]
        public string TripClass { get; set; }
    }

    // Модель для получения ссылки на покупку
    public class ClickResponseV2
    {
        [JsonPropertyName("gate_id")]
        public int GateId { get; set; }

        [JsonPropertyName("agent_id")]
        public int AgentId { get; set; }

        [JsonPropertyName("click_id")]
        public long ClickId { get; set; }

        [JsonPropertyName("str_click_id")]
        public string StrClickId { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("params")]
        public Dictionary<string, object> Params { get; set; }

        [JsonPropertyName("expire_at_unix_sec")]
        public long ExpireAtUnixSec { get; set; }
    }

    // Упрощенная модель для фронтенда
    public class SimplifiedFlight
    {
        public string Id { get; set; }
        public string Signature { get; set; }
        public List<FlightSegment> Segments { get; set; }
        public List<SimplifiedProposal> Proposals { get; set; }
        public decimal MinPrice => Proposals?.Min(p => p.Price.Amount) ?? 0;
        public string Currency => Proposals?.FirstOrDefault()?.Price.Currency ?? "RUB";
        public int TotalDuration { get; set; }
        public int TransfersCount { get; set; }
        public bool IsDirect => TransfersCount == 0;
    }

    public class FlightSegment
    {
        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public string Airline { get; set; }
        public string FlightNumber { get; set; }
        public int Duration { get; set; }
        public string Aircraft { get; set; }
    }

    public class SimplifiedProposal
    {
        public string Id { get; set; }
        public string AgentId { get; set; }
        public string AgentName { get; set; }
        public Price Price { get; set; }
        public FlightTerms FlightTerms { get; set; }
    }
}