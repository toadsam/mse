// file: last-round-backend/src/main/java/com/lastround/backend/dto/leaderboard/LeaderboardEntry.java
package com.lastround.backend.dto.leaderboard;

import lombok.Builder;
import lombok.Getter;

@Getter
@Builder
public class LeaderboardEntry {
    private Long userId;
    private String nickname;
    private long totalMatches;
    private long totalWins;
    private double winRate;
}
