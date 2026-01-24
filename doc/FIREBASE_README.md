# FIREBASE KEY UPDATE

Over time, the file `betrader-v1-firebase.json` becomes outdated. To ensure proper communication betweenClient App and Firebase, it is necessary to update this file. Follow these steps:

1. Go to the Firebase Console:
   [Firebase Console - Service Accounts](https://console.firebase.google.com/u/0/project/betrader-v1/settings/serviceaccounts/adminsdk)

2. Navigate to:
   **Project Settings** → **Service Accounts** tab.


3. Generate a new private key:
  - Click on the **Generate New Private Key** button.
  - Download the JSON file.


4. Replace the old JSON file:
   - Overwrite the file located at `C:/dev/BetsTrading-Service/betrader-v1-firebase.json` with the newly downloaded file.


5. Verify the update:
   - Restart the application or service that relies on Firebase.
   - Test the Firebase functionality (e.g., sending notifications).

**Note:** Keep the private key secure. Do not share it or include it in source Git control!
