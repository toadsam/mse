// file: last-round-backend/src/main/java/com/lastround/backend/dto/augment/AugmentResponse.java
package com.lastround.backend.dto.augment;

import lombok.Builder;
import lombok.Getter;

@Getter
@Builder
public class AugmentResponse {
    private Long id;
    private String name;
    private String description;
    private String effectType;
}
