// file: last-round-backend/src/main/java/com/lastround/backend/service/AugmentService.java
package com.lastround.backend.service;

import com.lastround.backend.dto.augment.AugmentResponse;
import com.lastround.backend.repository.AugmentRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

// Service layer for the augment catalog — retrieves all available in-game augments.
@Service
@RequiredArgsConstructor
public class AugmentService {

    private final AugmentRepository augmentRepository;

    // Returns the full augment list; readOnly transaction avoids unnecessary dirty-checking.
    @Transactional(readOnly = true)
    public List<AugmentResponse> getAugments() {
        return augmentRepository.findAll().stream()
                .map(augment -> AugmentResponse.builder()
                        .id(augment.getId())
                        .name(augment.getName())
                        .description(augment.getDescription())
                        .effectType(augment.getEffectType())
                        .build())
                .toList();
    }
}
