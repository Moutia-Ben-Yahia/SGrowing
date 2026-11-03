namespace SGrowing.Api.Modules.Advertising.AudioDai;

public record AudioAdSlot(
    string SlotId,
    string ShowName,
    string EpisodeTitle,
    string PositionType, // PRE_ROLL, MID_ROLL, POST_ROLL
    int DurationSeconds,
    string AudioCodec, // AAC, OPUS, MP3
    string AcousticWatermarkHash,
    decimal CpmBidUsd,
    string TargetDma, // US-NYC, US-SFO, UK-LON, DE-BER
    string Status // INSERTED, SERVING, COMPLETED
);

public record AudioAttributionEvent(
    string AttributionId,
    string SlotId,
    string PromoCode,
    string DeviceType, // CARPLAY, SMART_SPEAKER, MOBILE_HEADPHONES
    string ConversionChannel, // DIRECT_SIGNUP, DEMO_BOOKED, CHECKOUT
    decimal AttributedRevenueUsd,
    DateTime Timestamp
);

public record AudioDaiOverview(
    int TotalImpressionsServed,
    double AudioCompletionRatePct, // e.g. 96.4%
    decimal TotalSpendUsd,
    decimal TotalAttributedRevenueUsd,
    double ReturnOnAdSpendRoas,
    List<AudioAdSlot> ActiveSlots,
    List<AudioAttributionEvent> RecentAttributions
);

public record InsertAudioAdRequest(
    string ShowName,
    string EpisodeTitle,
    string PositionType,
    int DurationSeconds,
    decimal CpmBidUsd,
    string TargetDma
);

public record RecordAudioAttributionRequest(
    string SlotId,
    string PromoCode,
    string DeviceType,
    string ConversionChannel,
    decimal AttributedRevenueUsd
);
