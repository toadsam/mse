package com.lastround.backend.dto.match;

import lombok.Builder;
import lombok.Getter;

import java.time.LocalDateTime;
import java.util.List;

@Getter
@Builder
public class MatchPlayerHistoryResponse {
    private Long userId;
    private String nickname;
    private String result;
    private Integer score;
    private Integer damageDealt;
    private String characterName;
    private LocalDateTime createdAt;
    private List<MatchPlayerAugmentResponse> augments;
}