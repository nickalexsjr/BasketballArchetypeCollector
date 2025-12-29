/**
 * Prepare CSV for Appwrite Import
 *
 * This script reads the raw player CSV and creates a formatted CSV
 * that matches your Appwrite collection schema.
 *
 * Run with: node prepare_import.js
 */

const fs = require('fs');
const path = require('path');

// Paths
const INPUT_CSV = path.join(__dirname, '..', '..', 'player_career_stats_FINAL.csv');
const OUTPUT_CSV = path.join(__dirname, '..', '..', 'players_appwrite_import.csv');

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

    if ((fn === 'michael' && ln === 'jordan') || (fn === 'lebron' && ln === 'james')) {
        return 'GOAT';
    }
    if (overall >= 94) return 'Legendary';
    if (overall >= 88) return 'Epic';
    if (overall >= 80) return 'Rare';
    if (overall >= 72) return 'Uncommon';
    return 'Common';
}

// Get era from draft year
function getEra(draftYear) {
    const year = parseInt(draftYear);
    if (!year) return 'Modern';
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

// Escape CSV value
function escapeCSV(value) {
    if (value === null || value === undefined) return '';
    const str = String(value);
    if (str.includes(',') || str.includes('"') || str.includes('\n')) {
        return `"${str.replace(/"/g, '""')}"`;
    }
    return str;
}

// Main
console.log('Reading input CSV...');
const csvContent = fs.readFileSync(INPUT_CSV, 'utf-8');
const players = parseCSV(csvContent);
console.log(`Found ${players.length} players`);

// Create output CSV with Appwrite-compatible format
// Header must match your collection attributes
const header = '$id,name,era,rarity,ppg,rpg,apg,spg,bpg';

const rows = players.map(player => {
    const overall = calculateOverall(player);
    const rarity = determineRarity(overall, player.first_name, player.last_name);
    const era = getEra(player.draft_year);
    const name = `${player.first_name} ${player.last_name}`.trim();

    // Skip empty names
    if (!name || name === ' ') return null;

    return [
        player.id || '',                          // $id (document ID)
        escapeCSV(name),                          // name
        era,                                      // era
        rarity,                                   // rarity
        parseFloat(player.ppg) || 0,              // ppg
        parseFloat(player.rpg) || 0,              // rpg
        parseFloat(player.apg) || 0,              // apg
        parseFloat(player.spg) || 0,              // spg
        parseFloat(player.bpg) || 0               // bpg
    ].join(',');
}).filter(row => row !== null);

const outputContent = header + '\n' + rows.join('\n');

fs.writeFileSync(OUTPUT_CSV, outputContent);

console.log(`\nCreated: ${OUTPUT_CSV}`);
console.log(`Total players: ${rows.length}`);

// Show rarity distribution
const rarityCount = { GOAT: 0, Legendary: 0, Epic: 0, Rare: 0, Uncommon: 0, Common: 0 };
players.forEach(p => {
    const overall = calculateOverall(p);
    const rarity = determineRarity(overall, p.first_name, p.last_name);
    rarityCount[rarity]++;
});

console.log('\nRarity Distribution:');
Object.entries(rarityCount).forEach(([rarity, count]) => {
    console.log(`  ${rarity}: ${count}`);
});

console.log('\n=== Next Steps ===');
console.log('1. Go to Appwrite Console > Databases > basketball-archetypes > players');
console.log('2. Click the "..." menu > Import Documents');
console.log('3. Upload: players_appwrite_import.csv');
console.log('4. Map columns and import!');
