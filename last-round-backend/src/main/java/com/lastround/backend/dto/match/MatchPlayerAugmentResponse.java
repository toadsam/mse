package com.lastround.backend.dto.match;

import lombok.Builder;
import lombok.Getter;

// Response DTO for one augment selection made by a player during a match.
@Getter
@Builder
public class MatchPlayerAugmentResponse {
    // augmentId may be null if the augment name didn't match a known DB entry.
    private Long augmentId;
    private String augmentName;
    private String effectType;
    // selectedOrder indicates the pick sequence (1st, 2nd, ...) within the match.
    private Integer selectedOrder;
    private Integer selectedRound;
}