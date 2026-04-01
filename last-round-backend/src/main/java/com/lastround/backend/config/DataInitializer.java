// file: last-round-backend/src/main/java/com/lastround/backend/config/DataInitializer.java
package com.lastround.backend.config;

import com.lastround.backend.entity.Augment;
import com.lastround.backend.repository.AugmentRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.boot.CommandLineRunner;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.util.List;

@Configuration
@RequiredArgsConstructor
public class DataInitializer {

    private final AugmentRepository augmentRepository;

    @Bean
    public CommandLineRunner seedAugments() {
        return args -> {
            if (augmentRepository.count() > 0) {
                return;
            }

            List<Augment> augments = List.of(
                    Augment.builder().name("Fast Reload").description("Reload speed increases by 30%.").effectType("RELOAD_SPEED").build(),
                    Augment.builder().name("Explosive Shot").description("Bullets deal splash damage in a small radius.").effectType("BULLET_EFFECT").build(),
                    Augment.builder().name("Dash Charge").description("Gain one extra dash charge per round.").effectType("MOVEMENT").build(),
                    Augment.builder().name("Reflective Shield").description("Reflects 10% incoming damage.").effectType("DEFENSE").build(),
                    Augment.builder().name("Med Boost").description("Medkit restores an additional 25 HP.").effectType("HEALING").build()
            );

            augmentRepository.saveAll(augments);
        };
    }
}
