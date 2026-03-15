#!/usr/bin/env node
/**
 * Copy mermaid assets from node_modules to the Margin folder
 * This script runs after npm install to ensure the latest mermaid version is available
 */

const fs = require('fs');
const path = require('path');

const nodeModulesPath = path.join(__dirname, '..', 'node_modules', 'mermaid', 'dist');
const marginPath = path.join(__dirname, '..', 'src', 'Margin');

// Ensure the Margin directory exists
if (!fs.existsSync(marginPath)) {
    console.error(`Error: Margin directory not found at ${marginPath}`);
    process.exit(1);
}

// Files to copy
const filesToCopy = [
    'mermaid.min.js',
    'mermaid.min.js.map'
];

try {
    filesToCopy.forEach(file => {
        const srcPath = path.join(nodeModulesPath, file);
        const destPath = path.join(marginPath, file);

        if (!fs.existsSync(srcPath)) {
            console.warn(`Warning: Source file not found: ${srcPath}`);
            return;
        }

        fs.copyFileSync(srcPath, destPath);
        console.log(`? Copied ${file}`);
    });

    // Copy license file from mermaid package
    const licenseSourcePath = path.join(__dirname, '..', 'node_modules', 'mermaid', 'LICENSE');
    const licenseDestPath = path.join(marginPath, 'mermaid.min.js.LICENSE.txt');
    
    if (fs.existsSync(licenseSourcePath)) {
        fs.copyFileSync(licenseSourcePath, licenseDestPath);
        console.log(`? Copied mermaid.min.js.LICENSE.txt`);
    }

    console.log('\n? Mermaid assets successfully copied to src/Margin/');
} catch (error) {
    console.error('Error copying mermaid assets:', error.message);
    process.exit(1);
}
