# SyncOne

**SyncOne** is a modular and extensible application designed to manage SMS communication and processing in real-time. It supports seamless operation through a combination of background services and user interface integration, ensuring reliable messaging workflows. SyncOne is ideal for scenarios requiring message automation, real-time updates, and platform independence.

---

## Features

- **Background Processing:**
  - A robust foreground service (`SmsService`) processes SMS messages independently of the UI.
  - Ensures messages are sent and processed even when the application is not active.

- **Platform-Agnostic Design:**
  - Uses `ISmsService` as an abstraction layer for SMS operations, enabling seamless integration across platforms.
  - `AndroidSmsService` provides platform-specific implementation for Android.

- **Real-Time Updates:**
  - The UI reflects real-time updates of received and processed messages via `MainViewModel`.

- **Retry Mechanism:**
  - Built-in retry logic ensures reliable SMS delivery with exponential backoff for failed attempts.

- **Modular Architecture:**
  - Components like `DatabaseService`, `ApiService`, and `ConfigurationService` encapsulate specific responsibilities, promoting maintainability.

---

## Components

### 1. **AndroidSmsService**
- **Role:**
  - Handles platform-specific SMS operations such as sending and receiving messages.
  - Implements the `ISmsService` interface.
- **Features:**
  - Real-time SMS receiving using Android broadcast receivers.
  - SMS delivery and sent status tracking using `PendingIntent`.

### 2. **SmsService**
- **Role:**
  - Background service that ensures SMS processing continues even if the app is closed.
- **Features:**
  - Loads unprocessed messages from the database.
  - Uses `ISmsService` for sending SMS.
  - Implements a retry mechanism for failed message deliveries.

### 3. **MainViewModel**
- **Role:**
  - Manages UI state and interactions.
- **Features:**
  - Binds to the message list, reflecting real-time updates from `AndroidSmsService`.
  - Exposes commands like refresh to reload messages from the database.

---

## Prerequisites

1. **Android Development Environment:**
   - Android Studio or equivalent IDE.
   - Android SDK.

2. **Permissions:**
   - Add the following permissions to `AndroidManifest.xml`:
     ```xml
     <uses-permission android:name="android.permission.SEND_SMS"/>
     <uses-permission android:name="android.permission.RECEIVE_SMS"/>
     <uses-permission android:name="android.permission.READ_SMS"/>
     ```

3. **Dependency Injection:**
   - Ensure DI setup (e.g., Microsoft.Extensions.DependencyInjection) for services like `ISmsService`.

---

## Setup and Installation

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/yourusername/SyncOne.git
   ```

2. **Open the Project:**
   - Use Visual Studio or any compatible IDE to open the project.

3. **Configure Dependencies:**
   - Register services in your DI container:
     ```csharp
     services.AddSingleton<ISmsService, AndroidSmsService>();
     services.AddSingleton<DatabaseService>();
     services.AddSingleton<ApiService>();
     services.AddSingleton<ConfigurationService>();
     ```

4. **Build and Run:**
   - Build the project and deploy it to an Android device or emulator.

---

## Usage

### Running the Application
- Start the app to initialize the `SmsService`.
- The background service will handle SMS processing even if the app is minimized or closed.

### Using the UI
- The message list displays incoming and processed messages.
- Use the refresh command to reload messages from the database.

---

## Contributing

Contributions are welcome! To contribute:
1. Fork the repository.
2. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. Commit your changes:
   ```bash
   git commit -m "Add your message here"
   ```
4. Push to your branch:
   ```bash
   git push origin feature/your-feature-name
   ```
5. Open a Pull Request.

---

## License

SyncOne is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

## Contact

For questions or support, reach out to:
- **Email:** tellikoroma@gmail.com
- **GitHub Issues:** [SyncOne Issues](https://github.com/telli/SyncOne/issues)

