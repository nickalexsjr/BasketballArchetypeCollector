/**
 * Player Import Script for Basketball Archetype Collector
 *
 * This script imports players from CSV to Appwrite database.
 * Run with: node import_players.js
 */

const { Client, Databases, ID } = require('node-appwrite');
const fs = require('fs');
const path = require('path');

// Configuration - UPDATE THESE VALUES
const APPWRITE_ENDPOINT = 'https://cloud.appwrite.io/v1';
const APPWRITE_PROJECT_ID = 'basketballarchetype';
const APPWRITE_API_KEY = 'standard_ee9bd4e0c63457cb0444ba3b642bd3a88e8bd4729ace25b79fbd9d8c1a96ce1d8fdc941007aec1e07d283d12d43ffaf44151b6f726476ebd08f3c4b3109f14fbca37eb9ccc563bd01b7d73b8789a8561971d268303fd19b8be83ba3087becb37a69a0d0f7ec9ad2071a45c4d6910e34268fa74b19003e5d7b3219ce514eb23c6';
const DATABASE_ID = 'basketball-archetypes';
const PLAYERS_COLLECTION_ID = 'players';

// CSV file path
const CSV_PATH = path.join(__dirname, '..', '..', 'player_career_stats_FINAL.csv');

// Initialize Appwrite client
const client = new Client()
    .setEndpoint(APPWRITE_ENDPOINT)
    .setProject(APPWRITE_PROJECT_ID)
    .setKey(APPWRITE_API_KEY);

const databases = new Databases(client);

// Calculate overall rating (matches HTML logic)
function calculateOverall(player) {
    const ppg = parseFloat(player.ppg) || 0;
    const rpg = parseFloat(player.rpg) || 0;
    const apg = parseFloat(player.apg) || 0;
    const spg = parseFloat(player.spg) || 0;
    const bpg = parseFloat(player.bpg) || 0;
    const fgPct = parseFloat(player.fg_pct) || 0;
    const games = parseInt(player.games) || 0;

    const ptsScore = Math.min(100, (ppg / 25) * 100);
    const rebScore = Math.min(100, (rpg / 10) * 100);
    const astScore = Math.min(100, (apg / 8) * 100);
    const defScore = Math.min(100, ((spg + bpg) / 2.5) * 100);
    const effScore = fgPct > 0 ? Math.min(100, (fgPct / 0.50) * 100) : 50;

    const longevityBonus = Math.min(3, (games / 1200) * 3);

    const rawScore = (ptsScore * 0.40) + (rebScore * 0.15) + (astScore * 0.18) + (defScore * 0.12) + (effScore * 0.15);
    let overall = 52 + (rawScore * 0.47) + longevityBonus;

    return Math.min(98, Math.max(60, Math.round(overall)));
}

// Determine rarity (matches HTML logic)
function determineRarity(overall, firstName, lastName) {
    const fn = (firstName || '').toLowerCase();
    const ln = (lastName || '').toLowerCase();

    // GOAT rarity for MJ and LeBron only
    if ((fn === 'michael' && ln === 'jordan') || (fn === 'lebron' && ln === 'james')) {
        return 'GOAT';
    }
    if (overall >= 94) return 'Legendary';
    if (overall >= 88) return 'Epic';
    if (overall >= 80) return 'Rare';
    if (overall >= 72) return 'Uncommon';
    return 'Common';
}

// Get era from draft year (matches HTML logic)
function getEra(draftYear) {
    const year = parseInt(draftYear);
    if (!year) return 'Modern'; // Default for unknown
    if (year >= 2020) return 'Modern';
    if (year >= 2010) return '2010s';
    if (year >= 2000) return '2000s';
    if (year >= 1990) return '90s';
    if (year >= 1980) return '80s';
    return 'Classic';
}

// Parse CSV
function parseCSV(csvContent) {
    const lines = csvContent.trim().split('\n');
    const headers = lines[0].split(',');

    return lines.slice(1).map(line => {
        const values = [];
        let current = '';
        let inQuotes = false;

        for (let char of line) {
            if (char === '"') {
                inQuotes = !inQuotes;
            } else if (char === ',' && !inQuotes) {
                values.push(current.trim());
                current = '';
            } else {
                current += char;
            }
        }
        values.push(current.trim());

        const player = {};
        headers.forEach((header, i) => {
            player[header] = values[i] || '';
        });
        return player;
    });
}

// Import players to Appwrite
async function importPlayers() {
    console.log('Reading CSV file...');
    const csvContent = fs.readFileSync(CSV_PATH, 'utf-8');
    const players = parseCSV(csvContent);

    console.log(`Found ${players.length} players in CSV`);

    let imported = 0;
    let skipped = 0;
    let errors = 0;

    // Process in batches of 50
    const BATCH_SIZE = 50;

    for (let i = 0; i < players.length; i += BATCH_SIZE) {
        const batch = players.slice(i, i + BATCH_SIZE);

        const promises = batch.map(async (player) => {
            try {
                const overall = calculateOverall(player);
                const rarity = determineRarity(overall, player.first_name, player.last_name);
                const era = getEra(player.draft_year);

                const name = `${player.first_name} ${player.last_name}`.trim();

                // Skip players with no name
                if (!name || name === ' ') {
                    skipped++;
                    return;
                }

                const documentData = {
                    name: name,
                    era: era,
                    rarity: rarity,
                    ppg: parseFloat(player.ppg) || 0,
                    rpg: parseFloat(player.rpg) || 0,
                    apg: parseFloat(player.apg) || 0,
                    spg: parseFloat(player.spg) || 0,
                    bpg: parseFloat(player.bpg) || 0
                };

                // Use player ID from CSV as document ID
                const docId = player.id || ID.unique();

                await databases.createDocument(
                    DATABASE_ID,
                    PLAYERS_COLLECTION_ID,
                    docId,
                    documentData
                );

                imported++;
            } catch (err) {
                if (err.code === 409) {
                    // Document already exists, skip
                    skipped++;
                } else {
                    console.error(`Error importing ${player.first_name} ${player.last_name}: ${err.message}`);
                    errors++;
                }
            }
        });

        await Promise.all(promises);

        const progress = Math.min(100, Math.round(((i + batch.length) / players.length) * 100));
        console.log(`Progress: ${progress}% (${imported} imported, ${skipped} skipped, ${errors} errors)`);
    }

    console.log('\n=== Import Complete ===');
    console.log(`Total: ${players.length}`);
    console.log(`Imported: ${imported}`);
    console.log(`Skipped: ${skipped}`);
    console.log(`Errors: ${errors}`);
}

// Run import
importPlayers().catch(console.error);
