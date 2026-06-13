// file: last-round-backend/src/main/java/com/lastround/backend/LastRoundBackendApplication.java
package com.lastround.backend;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;

// Entry point of the Last Round backend Spring Boot application.
@SpringBootApplication
public class LastRoundBackendApplication {

    // Bootstraps the Spring application context and starts the embedded server.
    public static void main(String[] args) {
        SpringApplication.run(LastRoundBackendApplication.class, args);
    }
}
