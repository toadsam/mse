// file: last-round-backend/src/main/java/com/lastround/backend/dto/match/MatchHistoryResponse.java
package com.lastround.backend.dto.match;

import lombok.Builder;
import lombok.Getter;

import java.util.List;

@Getter
@Builder
public class MatchHistoryResponse {
    private List<MatchResponse> content;
    private int page;
    private int size;
    private long totalElements;
    private int totalPages;
}
