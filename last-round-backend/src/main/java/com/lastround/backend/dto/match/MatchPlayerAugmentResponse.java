package com.lastround.backend.dto.match;

import lombok.Builder;
import lombok.Getter;

@Getter
@Builder
public class MatchPlayerAugmentResponse {
    private Long augmentId;
    private String augmentName;
    private String effectType;
    private Integer selectedOrder;
    private Integer selectedRound;
}