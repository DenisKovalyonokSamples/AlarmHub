-- One database per service: no shared tables, no cross-service joins.
CREATE DATABASE alarmhub_ingest;
CREATE DATABASE alarmhub_processing;
CREATE DATABASE alarmhub_notifications;
CREATE DATABASE alarmhub_identity;
