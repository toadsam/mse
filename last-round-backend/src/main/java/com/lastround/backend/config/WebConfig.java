// file: last-round-backend/src/main/java/com/lastround/backend/config/WebConfig.java
package com.lastround.backend.config;

import org.springframework.context.annotation.Configuration;
import org.springframework.web.servlet.config.annotation.CorsRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;

// Global CORS configuration — allows all origins so Unity clients and web frontends can connect.
@Configuration
public class WebConfig implements WebMvcConfigurer {

    // Applies to every endpoint; credentials (cookies/auth headers) are allowed for JWT usage.
    @Override
    public void addCorsMappings(CorsRegistry registry) {
        registry.addMapping("/**")
                .allowedOriginPatterns("*")
                .allowedMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .allowedHeaders("*")
                .allowCredentials(true);
    }
}
