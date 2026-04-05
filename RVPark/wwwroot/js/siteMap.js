// Global variables to store original viewBox and selected site
let originalViewBox = '0 0 800.33 601';
let selectedSiteKey = null;
let siteData = {};
let siteTypePriceCache = {};


// Site type constants
const SITE_TYPES = {
    PREMIUM_PULL_THROUGH: 1,
    PREMIUM_BACK_IN: 2,
    STANDARD: 3,
    TENT_OVERFLOW: 4,
    SHORT_TERM_STORAGE: 5,
    WAGON_WHEEL: 6,
    PARTIAL_HOOKUP: 7
};

// Helper to check if the SVG is at original zoom level
function isViewBoxAtOriginal(svg) {
    return svg.getAttribute('viewBox') === originalViewBox;
}

// Escapes CSS selectors for numeric IDs
function escapeCSSSelector(id) {
    return id.replace(/([0-9])/g, '\\3$1 ');
}

// Loads the SVG map
async function loadSvgMap() {
    try {
        const response = await fetch('/svg/campsite.svg');
        if (!response.ok) {
            throw new Error(`HTTP error! Status: ${response.status}`);
        }
        const svgText = await response.text();
        const svgMap = document.getElementById('svg-map');
        if (svgMap) {
            svgMap.innerHTML = svgText;
            console.log('SVG map loaded successfully');
            initializeMap();
            if (Object.keys(siteData).length > 0) {
                updateMap(Object.values(siteData), parseFloat(document.getElementById('trailerLength')?.value) || 0);
            }
            const svgElement = document.querySelector('.svg-map svg');
            if (svgElement) {
                let [vbX, vbY, vbW, vbH] = (svgElement.getAttribute('viewBox') || '0 0 800.33 601').split(' ').map(Number);
                const fullViewBox = { x: vbX, y: vbY, w: vbW, h: vbH };
                setupMiniMap(svgElement, fullViewBox);
            }
        } else {
            console.warn('SVG map container not found');
        }
    } catch (error) {
        console.error('Error loading SVG:', error);
        document.getElementById('svg-map').innerHTML = '<p class="text-danger">Error loading map</p>';
    }
}

// Initializes the map with event listeners
function initializeMap() {
    const svgElement = document.querySelector('.svg-map svg');
    if (!svgElement) {
        console.warn('SVG element not found');
        return;
    }

    originalViewBox = svgElement.getAttribute('viewBox') || originalViewBox;

    const siteElements = svgElement.querySelectorAll('[id^="T"], [id]');
    console.log(`Found ${siteElements.length} site elements in SVG`);

    const zoomOutBtn = document.getElementById('zoom-out-btn');
    if (zoomOutBtn) {
        zoomOutBtn.disabled = true;
        zoomOutBtn.addEventListener('click', resetZoom);
    }

    const tooltip = document.getElementById('site-tooltip');
    siteElements.forEach(site => {
        site.addEventListener('click', () => {
            if (site.classList.contains('site-available')) {
                selectSite(site.id, true); // Zoom on click
            }
        });

        site.addEventListener('mouseover', async (e) => {
            const siteInfo = siteData[site.id];

            if (!site.classList.contains('site-available') &&
                !site.classList.contains('site-selected')) {
                return;
            }

            const maxTrailer = siteInfo && siteInfo.trailerMaxSize
                ? `${siteInfo.trailerMaxSize} ft`
                : 'N/A';

            const siteType = siteInfo && siteInfo.siteType
                ? siteInfo.siteType
                : 'N/A';

            let priceLabelText = 'N/A';

            const startInput = document.getElementById('startDate');
            const endInput = document.getElementById('endDate');
            const startDate = startInput ? startInput.value : '';
            const endDate = endInput ? endInput.value : '';

            if (siteInfo && startDate && endDate) {
                const range = await getSiteTypePriceRange(siteInfo.siteTypeId, startDate, endDate);

                if (range && typeof range.minRate === 'number' && typeof range.maxRate === 'number' &&
                    range.minRate > 0 && range.maxRate > 0) {

                    if (range.minRate === range.maxRate) {
                        priceLabelText = `$${range.minRate.toFixed(2)}/night`;
                    } else {
                        // priceLabelText = `Starting at $${range.minRate.toFixed(2)}/night`;
                        priceLabelText = `$${range.minRate.toFixed(2)} - $${range.maxRate.toFixed(2)} per night`;
                    }
                } else if (siteInfo.pricePerDay) {
                    priceLabelText = `$${siteInfo.pricePerDay.toFixed(2)}/night`;
                }
            } else if (siteInfo && siteInfo.pricePerDay) {
                priceLabelText = `$${siteInfo.pricePerDay.toFixed(2)}/night`;
            }

            tooltip.innerHTML = `
        <strong>Price: ${priceLabelText}</strong><br>
        Max Trailer: ${maxTrailer}<br>
        Type: ${siteType}<br>
        ${siteInfo && siteInfo.isHandicappedAccessible ? '<strong>Handicapped Accessible</strong>' : ''}
    `;

            tooltip.style.display = 'block';
            positionTooltip(e, tooltip);
        });


        site.addEventListener('mousemove', (e) => {
            positionTooltip(e, tooltip);
        });

        site.addEventListener('mouseout', () => {
            tooltip.style.display = 'none';
        });
    });

    function positionTooltip(e, tooltip) {
        const offsetX = 10;
        const offsetY = 10;
        let left = e.clientX + offsetX;
        let top = e.clientY + offsetY;

        const tooltipRect = tooltip.getBoundingClientRect();
        const modalRect = document.querySelector('.modal-content').getBoundingClientRect();

        if (left + tooltipRect.width > modalRect.right - 10) {
            left = e.clientX - tooltipRect.width - offsetX;
        }

        if (top + tooltipRect.height > modalRect.bottom - 10) {
            top = e.clientY - tooltipRect.height - offsetY;
        }

        left = Math.max(10, left);
        left = Math.max(modalRect.left + 10, left);
        top = Math.max(modalRect.top + 10, top);

        tooltip.style.left = `${left}px`;
        tooltip.style.top = `${top}px`;
    }

    const chosenSiteId = document.getElementById('chosenSiteId');
    if (chosenSiteId) {
        chosenSiteId.addEventListener('change', () => {
            const selectedValue = chosenSiteId.value;
            if (selectedValue) {
                const site = Object.values(siteData).find(s => s.siteId.toString() === selectedValue.toString());
                if (site) {
                    selectSite(site.name, false); // No zoom on dropdown change
                } else {
                    console.warn(`Site with ID ${selectedValue} not found in siteData`);
                    deselectSite();
                }
            } else {
                deselectSite();
            }
        });
    }

    const inputs = ['startDate', 'endDate'].map(id => document.getElementById(id)).filter(Boolean);
    const trailerLengthInput = document.getElementById('trailerLength');
    const checkboxes = document.querySelectorAll('.site-type-checkbox');
    const showHandicapped = document.getElementById('showHandicapped');
    const findButton = document.getElementById('findAvailableSites');

    inputs.forEach(input => {
        input.addEventListener('change', () => {
            const modal = bootstrap.Modal.getInstance(document.getElementById('siteMapModal'));
            if (modal) modal.hide();
            fetchAvailableSites();
        });
    });

    if (trailerLengthInput) {
        trailerLengthInput.addEventListener('input', handleTrailerLengthChange);
        trailerLengthInput.addEventListener('change', () => {
            const modal = bootstrap.Modal.getInstance(document.getElementById('siteMapModal'));
            if (modal) modal.hide();
            fetchAvailableSites();
        });
    }

    checkboxes.forEach(checkbox => {
        checkbox.addEventListener('change', () => {
            const modal = bootstrap.Modal.getInstance(document.getElementById('siteMapModal'));
            if (modal) modal.hide();
            fetchAvailableSites();
        });
    });

    const selectAllBtn = document.getElementById('selectAllSiteTypes');
    const deselectAllBtn = document.getElementById('deselectAllSiteTypes');

    if (selectAllBtn) {
        selectAllBtn.addEventListener('click', () => {
            document.querySelectorAll('.site-type-checkbox').forEach(cb => {
                if (!cb.disabled) cb.checked = true;
            });
            fetchAvailableSites();
        });
    }

    if (deselectAllBtn) {
        deselectAllBtn.addEventListener('click', () => {
            document.querySelectorAll('.site-type-checkbox').forEach(cb => {
                cb.checked = false;
            });
            fetchAvailableSites();
        });
    }

    if (showHandicapped) {
        showHandicapped.addEventListener('change', () => {
            const modal = bootstrap.Modal.getInstance(document.getElementById('siteMapModal'));
            if (modal) modal.hide();
            fetchAvailableSites();
        });
    }

    if (findButton) {
        findButton.addEventListener('click', fetchAvailableSites);
    }
}

// Handles trailer length input changes
function handleTrailerLengthChange() {
    const trailerLength = document.getElementById('trailerLength')?.value;
    const tentOverflowCheckbox = document.querySelector('.site-type-checkbox[value="4"]');
    const standardCheckbox = document.querySelector('.site-type-checkbox[value="3"]');

    // Reset disabled state every time
    [tentOverflowCheckbox, standardCheckbox].forEach(cb => {
        if (!cb) return;
        cb.disabled = false;
        cb.title = '';
    });

    if (trailerLength && parseFloat(trailerLength) > 0) {
        if (tentOverflowCheckbox && tentOverflowCheckbox.checked) {
            tentOverflowCheckbox.checked = false;
            console.log('Unchecked Tent/Overflow due to trailer length');
        }
        if (trailerLength > 45 && standardCheckbox && standardCheckbox.checked) {
            standardCheckbox.checked = false;
            console.log('Unchecked Standard due to trailer length > 45');
        }
    }

    if (trailerLength && parseFloat(trailerLength) > 0) {
        // Tent/Overflow is never valid for trailers
        if (tentOverflowCheckbox) {
            tentOverflowCheckbox.checked = false;
            tentOverflowCheckbox.disabled = true;
            tentOverflowCheckbox.title = 'Not available for trailers';
        }

        // Standard has 45 ft limit
        if (standardCheckbox && trailerLength > 45) {
            standardCheckbox.checked = false;
            standardCheckbox.disabled = true;
            standardCheckbox.title = 'Trailer too long for Standard site (max 45 ft)';
        }
    }

    setTimeout(() => {
        fetchAvailableSites();
    }, 100);
}

// Fetches available sites from the API
async function fetchAvailableSites() {
    const startDate = document.getElementById('startDate')?.value;
    const endDate = document.getElementById('endDate')?.value;
    const trailerLength = document.getElementById('trailerLength')?.value || '';
    const selectedSiteTypes = Array.from(document.querySelectorAll('.site-type-checkbox:checked')).map(cb => parseInt(cb.value));
    const showHandicapped = document.getElementById('showHandicapped')?.checked ?? true;
    const isAdminPage = window.location.pathname.includes('/Admin/Reservations/');

    console.log('fetchAvailableSites called with:', {
        startDate,
        endDate,
        trailerLength,
        selectedSiteTypes,
        showHandicapped
    });

    if (!startDate || !endDate || (selectedSiteTypes.length === 0 && !showHandicapped)) {
        console.warn('Missing required parameters for API call');
        // Still update the map to show no sites available
        const modal = bootstrap.Modal.getInstance(document.getElementById('siteMapModal'));
        if (modal && modal._isShown) {
            updateMap([], parseFloat(trailerLength) || 0);
        }
        return;
    }

    //logic to handle handicapped filter properly
    let apiSiteTypes = selectedSiteTypes.slice();

    if (showHandicapped && selectedSiteTypes.length > 0) {
        const rvSiteTypes = [1, 2, 3, 7]; // All RV site types that can have handicapped sites
        rvSiteTypes.forEach(typeId => {
            if (!apiSiteTypes.includes(typeId)) {
                apiSiteTypes.push(typeId);
            }
        });
        console.log('Handicapped filter ON with selected types, expanded API call to include all RV types:', apiSiteTypes);
    } else if (selectedSiteTypes.length === 0 && showHandicapped) {
        apiSiteTypes = [1, 2, 3, 4, 7]; // All site types except storage and wagon wheel
        console.log('No site types selected but handicapped filter on, using all types:', apiSiteTypes);
    }

    try {
        const response = await fetch(`${window.location.origin}/api/Reservation/GetAvailableSites?startDate=${startDate}&endDate=${endDate}&siteTypeIds=${apiSiteTypes.join(',')}&trailerLength=${trailerLength}`);
        if (!response.ok) {
            throw new Error(`HTTP error! Status: ${response.status}`);
        }
        const data = await response.json();
        console.log('API returned', data.sites.length, 'total sites');

        let filteredSites = data.sites.filter(site =>
            site.siteTypeId !== SITE_TYPES.SHORT_TERM_STORAGE &&
            (isAdminPage || site.siteTypeId !== SITE_TYPES.WAGON_WHEEL)
        );

        console.log('After basic filtering:', filteredSites.length, 'sites');
        console.log('showHandicapped:', showHandicapped);

        // filtering logic for handicapped sites
        if (showHandicapped && selectedSiteTypes.length > 0) {
            // When handicapped filter is ON with specific site types:
            // Include sites that match the selected types OR are handicapped accessible
            filteredSites = filteredSites.filter(site =>
                selectedSiteTypes.includes(site.siteTypeId) || site.isHandicappedAccessible
            );
            console.log('After handicapped + selected types filtering:', filteredSites.length, 'sites');
        } else if (!showHandicapped) {
            // When handicapped filter is OFF: exclude handicapped sites AND filter by selected types
            filteredSites = filteredSites.filter(site =>
                !site.isHandicappedAccessible &&
                (selectedSiteTypes.length === 0 || selectedSiteTypes.includes(site.siteTypeId))
            );
            console.log('After non-handicapped filtering:', filteredSites.length, 'sites');
        } else if (selectedSiteTypes.length === 0 && showHandicapped) {
            // Handicapped-only mode: show only handicapped sites
            filteredSites = filteredSites.filter(site => site.isHandicappedAccessible);
            console.log('After handicapped-only filtering:', filteredSites.length, 'sites');
        } else {
            // Normal filtering by selected site types only
            filteredSites = filteredSites.filter(site =>
                selectedSiteTypes.length === 0 || selectedSiteTypes.includes(site.siteTypeId)
            );
            console.log('After site type filtering:', filteredSites.length, 'sites');
        }

        // Update for the global siteData object and window.siteData
        siteData = {};
        window.siteData = {};
        filteredSites.forEach(site => {
            const siteInfo = {
                siteId: site.siteId.toString(),
                name: site.name,
                trailerMaxSize: site.trailerMaxSize,
                siteType: site.siteType,
                siteTypeId: site.siteTypeId,
                pricePerDay: site.pricePerDay,
                isHandicappedAccessible: site.isHandicappedAccessible
            };
            siteData[site.name] = siteInfo;
            window.siteData[site.name] = siteInfo;
        });

        console.log('Updated siteData with', Object.keys(siteData).length, 'sites');
        console.log('Site types in siteData:', [...new Set(Object.values(siteData).map(s => s.siteTypeId))]);
        console.log('Handicapped sites in siteData:', Object.values(siteData).filter(s => s.isHandicappedAccessible).length);

        const modal = bootstrap.Modal.getInstance(document.getElementById('siteMapModal'));
        if (modal && modal._isShown) {
            updateMap(filteredSites, parseFloat(trailerLength) || 0);
        }
    } catch (error) {
        console.error('Error fetching available sites:', error);
        const modal = bootstrap.Modal.getInstance(document.getElementById('siteMapModal'));
        if (modal && modal._isShown) {
            updateMap([], parseFloat(trailerLength) || 0);
        }
    }
}

// Fetches price range for a given site type and date range

async function getSiteTypePriceRange(siteTypeId, startDate, endDate) {
    if (!siteTypeId || !startDate || !endDate) {
        return null;
    }

    const cacheKey = `${siteTypeId}|${startDate}|${endDate}`;
    if (siteTypePriceCache[cacheKey]) {
        return siteTypePriceCache[cacheKey];
    }

    try {
        const response = await fetch(
            `${window.location.origin}/api/Reservation/GetSiteTypeRateRange` +
            `?siteTypeId=${siteTypeId}&startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`
        );

        if (!response.ok) {
            console.error('Failed to get site type rate range:', response.status);
            return null;
        }

        const data = await response.json();
        siteTypePriceCache[cacheKey] = data;
        return data;
    } catch (error) {
        console.error('Error calling GetSiteTypeRateRange:', error);
        return null;
    }
}


// Updates the SVG map to reflect site availability
function updateMap(sites, trailerLength = 0) {
    const svgElement = document.querySelector('.svg-map svg');
    const isAdminPage = window.location.pathname.includes('/Admin/Reservations/');

    if (!svgElement) {
        console.warn('SVG element not found in DOM');
        return;
    }

    if (window.isCheckAvailabilityTriggered || !selectedSiteKey) {
        svgElement.setAttribute('viewBox', originalViewBox);
        console.log('Reset viewBox to original:', originalViewBox);
        const zoomOutBtn = document.getElementById('zoom-out-btn');
        if (zoomOutBtn) zoomOutBtn.disabled = true;
    } else {
        console.log('Preserving viewBox due to selected site:', selectedSiteKey);
    }

    window.isCheckAvailabilityTriggered = false;

    const allSites = svgElement.querySelectorAll('[id]');
    console.log(`Found ${allSites.length} site elements in SVG`);

    // If no sites are provided, mark all as unavailable
    if (!sites || sites.length === 0) {
        allSites.forEach(siteElement => {
            siteElement.classList.remove('site-available', 'site-unavailable', 'site-selected');
            siteElement.classList.add('site-unavailable');
            siteElement.style.cursor = 'default';
            siteElement.title = 'Site unavailable due to failed data fetch';
        });

        // Update mini map after marking all sites as unavailable
        setTimeout(() => {
            updateMiniMap();
        }, 100);
        return;
    }

    // Create a map for quick lookup filtered by fetchAvailableSites
    const siteDataMap = new Map();
    sites.forEach(site => {
        siteDataMap.set(site.name, site);
    });



    allSites.forEach(siteElement => {
        const siteId = siteElement.id;
        const actualSite = siteDataMap.get(siteId);

        siteElement.classList.remove('site-available', 'site-unavailable', 'site-selected');
        siteElement.style.cursor = '';
        siteElement.title = '';

        // Skip text elements and legend elements
        if (siteElement.tagName === 'text' || siteElement.classList.contains('cls-27')) {
            return;
        }

        // If site is not in our filtered list, mark as unavailable
        if (!actualSite) {
            siteElement.classList.add('site-unavailable');
            siteElement.style.cursor = 'default';
            siteElement.title = 'Site unavailable for selected criteria';
            return;
        }

        // Check if this is a Wagon Wheel site and we're not on admin page
        if (!isAdminPage && actualSite.siteTypeId === SITE_TYPES.WAGON_WHEEL) {
            siteElement.classList.add('site-unavailable');
            siteElement.style.cursor = 'default';
            siteElement.title = 'Wagon Wheel sites available by phone only';
            return;
        }

        // Check tent/overflow restrictions with trailer length
        if (actualSite.siteTypeId === SITE_TYPES.TENT_OVERFLOW && trailerLength > 0) {
            siteElement.classList.add('site-unavailable');
            siteElement.style.cursor = 'default';
            siteElement.title = 'Tent sites not suitable for trailers';
            return;
        }

        // Check trailer length restrictions
        if (trailerLength > 0) {
            // Standard sites have 45ft limit
            if (actualSite.siteTypeId === SITE_TYPES.STANDARD && trailerLength > 45) {
                siteElement.classList.add('site-unavailable');
                siteElement.style.cursor = 'default';
                siteElement.title = 'Trailer too long for Standard site (max 45 ft)';
                return;
            }

            // Check individual site trailer max size
            if (actualSite.trailerMaxSize && trailerLength > actualSite.trailerMaxSize) {
                siteElement.classList.add('site-unavailable');
                siteElement.style.cursor = 'default';
                siteElement.title = `Trailer too long for this site (max ${actualSite.trailerMaxSize} ft)`;
                return;
            }
        }

        // If get here, the site is available
        siteElement.classList.add('site-available');
        siteElement.title = `Site ${actualSite.name} - Click to select${actualSite.isHandicappedAccessible ? ' (Handicapped Accessible)' : ''}`;
        siteElement.style.cursor = 'pointer';

        console.log(`Site ${actualSite.name} (Type: ${actualSite.siteTypeId}, Handicapped: ${actualSite.isHandicappedAccessible}) marked as available`);
    });

    // Don't apply classes to site numbers and handicapped symbols
    const siteNumbers = svgElement.querySelectorAll('#SiteNumbers text');
    siteNumbers.forEach(text => {
        text.classList.remove('site-available', 'site-unavailable', 'site-selected');
    });

    const handicappedSymbols = svgElement.querySelectorAll('#HandicappedAccessible path.cls-27');
    handicappedSymbols.forEach(symbol => {
        symbol.classList.remove('site-available', 'site-unavailable', 'site-selected');
    });

    // Handle previously selected site
    if (selectedSiteKey) {
        const selectedElement = svgElement.querySelector(`#${escapeCSSSelector(selectedSiteKey)}`);
        if (selectedElement && siteDataMap.has(selectedSiteKey) &&
            (isAdminPage || siteData[selectedSiteKey].siteTypeId !== SITE_TYPES.WAGON_WHEEL)) {
            selectedElement.classList.remove('site-available', 'site-unavailable');
            selectedElement.classList.add('site-selected');
            console.log('Site selected class applied to:', selectedSiteKey);
        } else {
            console.log('Previously selected site no longer available or restricted, clearing selection');
            selectedSiteKey = null;
            const siteDropdown = document.getElementById('chosenSiteId');
            if (siteDropdown) {
                window.isProgrammaticChange = true;
                siteDropdown.value = '';
                $(siteDropdown).trigger('change');
            }
            const siteInfo = document.getElementById('siteInfo');
            if (siteInfo) siteInfo.style.display = 'none';
        }
    }

    // Update mini map with current site states after main map is updated
    setTimeout(() => {
        updateMiniMap();
    }, 100);
}

// Displays details for the selected site
function showSiteDetails(site) {
    console.log('Showing details for site:', site);
    const siteInfo = document.getElementById('siteInfo');
    const siteInfoContent = document.getElementById('siteInfoContent');
    if (siteInfo && siteInfoContent) {
        siteInfo.style.display = 'block';
        siteInfoContent.innerHTML = `
            <p><strong>Name:</strong> ${site.name}</p>
            <p><strong>Description:</strong> ${site.description || 'No description available'}</p>
            <p><strong>Max Trailer Size:</strong> ${site.trailerMaxSize ? site.trailerMaxSize + ' ft' : 'N/A'}</p>
            <p><strong>Accessibility:</strong> ${site.isHandicappedAccessible ? 'Handicapped Accessible' : 'Standard'}</p>
        `;
        const siteDropdown = document.getElementById('chosenSiteId');
        if (siteDropdown) {
            window.isProgrammaticChange = true;
            siteDropdown.value = site.siteId;
            $(siteDropdown).trigger('change');
        }
    }
}

// Colorblind Mode Functionality
(function () {
    'use strict';

    // Initialize colorblind mode from localStorage
    function initializeColorblindMode() {
        const toggle = document.getElementById('colorblind-mode-toggle');
        if (!toggle) return;

        // Check if user has previously enabled colorblind mode
        const isColorblindMode = localStorage.getItem('colorblind-mode') === 'true';

        if (isColorblindMode) {
            document.body.classList.add('colorblind-mode');
            toggle.checked = true;
            console.log('Colorblind mode enabled from localStorage');
        }

        // Add event listener for toggle
        toggle.addEventListener('change', toggleColorblindMode);
    }

    // Toggle colorblind mode on/off
    function toggleColorblindMode() {
        const toggle = document.getElementById('colorblind-mode-toggle');
        const isEnabled = toggle.checked;

        if (isEnabled) {
            document.body.classList.add('colorblind-mode');
            localStorage.setItem('colorblind-mode', 'true');
            console.log('Colorblind mode enabled');

            // Show brief notification
            showColorblindModeNotification('Colorblind-friendly colors enabled');
        } else {
            document.body.classList.remove('colorblind-mode');
            localStorage.setItem('colorblind-mode', 'false');
            console.log('Colorblind mode disabled');

            // Show brief notification
            showColorblindModeNotification('Standard colors restored');
        }

        // Update any existing map visualization if present
        updateMapColors();
    }

    // Show a brief notification when mode changes
    function showColorblindModeNotification(message) {
        // Create notification element
        const notification = document.createElement('div');
        notification.className = 'colorblind-notification';
        notification.textContent = message;
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: rgba(0, 0, 0, 0.8);
            color: white;
            padding: 12px 20px;
            border-radius: 6px;
            font-size: 0.9rem;
            font-weight: 500;
            z-index: 10000;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
            transition: all 0.3s ease;
            transform: translateX(100%);
        `;

        document.body.appendChild(notification);

        // Animate in
        setTimeout(() => {
            notification.style.transform = 'translateX(0)';
        }, 10);

        // Remove after 3 seconds
        setTimeout(() => {
            notification.style.transform = 'translateX(100%)';
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 300);
        }, 3000);
    }

    // Update map colors if map is currently visible
    function updateMapColors() {
        const svg = document.querySelector('.svg-map svg');
        if (!svg) return;

        // Get the current state 
        const availableSites = svg.querySelectorAll('.site-available');
        const unavailableSites = svg.querySelectorAll('.site-unavailable');
        const selectedSites = svg.querySelectorAll('.site-selected');

        // Force a repaint by removing and re-adding classes
        [...availableSites, ...unavailableSites, ...selectedSites].forEach(site => {
            const classes = Array.from(site.classList);
            site.className = '';
            requestAnimationFrame(() => {
                classes.forEach(cls => site.classList.add(cls));
            });
        });

        console.log('Map colors updated for colorblind mode');
    }

    // Initialize when DOM is loaded
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeColorblindMode);
    } else {
        initializeColorblindMode();
    }

    // Initialize when modal is shown
    const modal = document.getElementById('siteMapModal');
    if (modal) {
        modal.addEventListener('shown.bs.modal', function () {
            //  delay 
            setTimeout(initializeColorblindMode, 100);
        });
    }

    window.initializeColorblindMode = initializeColorblindMode;
})();

// Selects a site on the map
// Selects a site on the map
function selectSite(siteKey, zoom = false) {
    const svgElement = document.querySelector('.svg-map svg');
    const isAdminPage = window.location.pathname.includes('/Admin/Reservations/');
    if (!svgElement) {
        console.error('SVG element not found in DOM');
        return;
    }

    if (!isAdminPage && siteData[siteKey] && siteData[siteKey].siteTypeId === SITE_TYPES.WAGON_WHEEL) {
        console.log('Wagon Wheel site cannot be selected on client page');
        return;
    }

    window.siteMapSelectionInProgress = true;

    const allSites = svgElement.querySelectorAll('[id]');
    allSites.forEach(el => {
        el.classList.remove('site-selected');
        const siteInfo = siteData[el.id];
        if (siteInfo) {
            if (!isAdminPage && siteInfo.siteTypeId === SITE_TYPES.WAGON_WHEEL) {
                el.classList.add('site-unavailable');
                el.style.cursor = 'default';
                el.title = 'Site unavailable for selected dates';
            } else {
                el.classList.add('site-available');
                el.style.cursor = 'pointer';
                el.title = `Site ${siteInfo.name} - Click to select`;
            }
        } else {
            el.classList.add('site-unavailable');
            el.style.cursor = 'default';
            el.title = 'Site unavailable for selected dates';
        }
    });

    const siteElement = svgElement.querySelector(`#${escapeCSSSelector(siteKey)}`);
    if (siteElement && siteData[siteKey] && (isAdminPage || siteData[siteKey].siteTypeId !== SITE_TYPES.WAGON_WHEEL)) {
        siteElement.classList.remove('site-available', 'site-unavailable');
        siteElement.classList.add('site-selected');
        selectedSiteKey = siteKey;
        console.log('Applied site-selected to:', siteKey, 'Classes:', siteElement.classList.toString());

        const siteDropdown = document.getElementById('chosenSiteId');
        if (siteDropdown && siteData[siteKey]) {
            siteDropdown.value = siteData[siteKey].siteId;
            showSiteDetails(siteData[siteKey]);
            $(siteDropdown).trigger('change');
        }

        if (zoom) {
            const bbox = siteElement.getBBox();
            console.log(`Selecting site ${siteKey}: bbox =`, bbox);
            if (bbox.width === 0 || bbox.height === 0) {
                console.warn(`Invalid bbox for ${siteKey}, using fallback zoom`);
                svgElement.setAttribute('viewBox', originalViewBox);
            } else {
                const padding = 50;
                const viewBox = `${bbox.x - padding} ${bbox.y - padding} ${bbox.width + 2 * padding} ${bbox.height + 2 * padding}`;
                svgElement.setAttribute('viewBox', viewBox);
                console.log('Set zoomed viewBox:', viewBox);
            }
        }

        // Explicitly enable zoom-out button after zooming
        const zoomOutBtn = document.getElementById('zoom-out-btn');
        if (zoomOutBtn) {
            zoomOutBtn.disabled = isViewBoxAtOriginal(svgElement);
            console.log('Zoom out button state updated:', zoomOutBtn.disabled ? 'disabled' : 'enabled');
        }
    } else {
        console.error(`Site ${siteKey} not found, not available, or restricted`);
        selectedSiteKey = null;
        const siteDropdown = document.getElementById('chosenSiteId');
        if (siteDropdown) {
            siteDropdown.value = '';
            $(siteDropdown).trigger('change');
        }
    }

    setTimeout(() => {
        updateMiniMap();
        window.siteMapSelectionInProgress = false;
        // Double-check zoom-out button state after mini map update
        const zoomOutBtn = document.getElementById('zoom-out-btn');
        if (zoomOutBtn) {
            zoomOutBtn.disabled = isViewBoxAtOriginal(svgElement);
            console.log('Zoom out button state after mini map update:', zoomOutBtn.disabled ? 'disabled' : 'enabled');
        }
    }, 150);
}

// Deselects the currently selected site
function deselectSite() {
    const svgElement = document.querySelector('.svg-map svg');
    if (!svgElement) return;

    const allSelected = svgElement.querySelectorAll('.site-selected');
    allSelected.forEach(el => {
        el.classList.remove('site-selected');
        if (siteData[el.id]) {
            el.classList.add('site-available');
        } else {
            el.classList.add('site-unavailable');
        }
    });

    selectedSiteKey = null;
    svgElement.setAttribute('viewBox', originalViewBox);
    console.log('Deselected site, reset viewBox to:', originalViewBox);

    const zoomOutBtn = document.getElementById('zoom-out-btn');
    if (zoomOutBtn) zoomOutBtn.disabled = true;

    const siteInfo = document.getElementById('siteInfo');
    if (siteInfo) siteInfo.style.display = 'none';
}

// Resets the zoom to the original viewBox
function resetZoom() {
    const svgElement = document.querySelector('.svg-map svg');
    if (svgElement) {
        svgElement.setAttribute('viewBox', originalViewBox);
        console.log('Zoom reset to:', originalViewBox);
        const zoomOutBtn = document.getElementById('zoom-out-btn');
        if (zoomOutBtn) zoomOutBtn.disabled = true;
    }
}

// Expose selectSiteOnMapById
window.selectSiteOnMapById = function (siteId, zoom = false) {
    setTimeout(() => {
        const site = Object.values(siteData).find(s => s.siteId.toString() === siteId.toString());
        if (site) {
            console.log('selectSiteOnMapById: Found site for siteId:', siteId, 'Name:', site.name, 'Site:', site);
            selectSite(site.name, zoom);
        } else {
            console.error('selectSiteOnMapById: No site found for siteId:', siteId);
            if (window.deselectSite) {
                window.deselectSite();
            }
        }
    }, 200);
};

// Enhanced SVG Map Navigation: Zoom & Pan
(function () {
    function setupEnhancedNavigation() {
        const svgContainer = document.querySelector('.svg-map');
        const svg = svgContainer.querySelector('svg');
        if (!svg) return;

        // Get initial viewBox
        let [vbX, vbY, vbW, vbH] = (svg.getAttribute('viewBox') || '0 0 800.33 601').split(' ').map(Number);
        const fullViewBox = { x: vbX, y: vbY, w: vbW, h: vbH };

        // State for pan/zoom
        let isPanning = false, startPoint = null, startViewBox = null;

        // --- Mouse Wheel Zoom ---
        svg.addEventListener('wheel', function (e) {
            e.preventDefault();
            const scaleFactor = 1.15;
            let [x, y, w, h] = svg.getAttribute('viewBox').split(' ').map(Number);

            // Get mouse position relative to SVG coordinate system
            const pt = svg.createSVGPoint();
            pt.x = e.clientX;
            pt.y = e.clientY;
            const svgP = pt.matrixTransform(svg.getScreenCTM().inverse());

            // Zoom in or out
            let zoom = e.deltaY < 0 ? 1 / scaleFactor : scaleFactor;
            let newW = w * zoom;
            let newH = h * zoom;

            // Clamp zoom
            const minW = fullViewBox.w * 0.1, maxW = fullViewBox.w;
            if (newW < minW) { newW = minW; newH = minW * h / w; }
            if (newW > maxW) { newW = maxW; newH = maxW * h / w; }

            // Center zoom on cursor
            let newX = svgP.x - ((svgP.x - x) * (newW / w));
            let newY = svgP.y - ((svgP.y - y) * (newH / h));

            // Clamp pan to bounds
            newX = Math.max(fullViewBox.x, Math.min(newX, fullViewBox.x + fullViewBox.w - newW));
            newY = Math.max(fullViewBox.y, Math.min(newY, fullViewBox.y + fullViewBox.h - newH));

            svg.setAttribute('viewBox', `${newX} ${newY} ${newW} ${newH}`);
            // Enable/disable zoom out button
            const zoomOutBtn = document.getElementById('zoom-out-btn');
            if (zoomOutBtn) zoomOutBtn.disabled = isViewBoxAtOriginal(svg);
        }, { passive: false });


        // --- Pan with Mouse Drag ---
        svg.addEventListener('mousedown', function (e) {
            if (e.button !== 0) return;
            isPanning = true;
            startPoint = { x: e.clientX, y: e.clientY };
            startViewBox = svg.getAttribute('viewBox').split(' ').map(Number);
            svg.style.cursor = 'grab';
        });
        window.addEventListener('mousemove', function (e) {
            if (!isPanning) return;
            let dx = (e.clientX - startPoint.x) * (startViewBox[2] / svg.clientWidth);
            let dy = (e.clientY - startPoint.y) * (startViewBox[3] / svg.clientHeight);
            let newX = startViewBox[0] - dx;
            let newY = startViewBox[1] - dy;

            // Clamp pan to bounds
            newX = Math.max(fullViewBox.x, Math.min(newX, fullViewBox.x + fullViewBox.w - startViewBox[2]));
            newY = Math.max(fullViewBox.y, Math.min(newY, fullViewBox.y + fullViewBox.h - startViewBox[3]));

            svg.setAttribute('viewBox', `${newX} ${newY} ${startViewBox[2]} ${startViewBox[3]}`);
            // Enable/disable zoom out button
            const zoomOutBtn = document.getElementById('zoom-out-btn');
            if (zoomOutBtn) zoomOutBtn.disabled = isViewBoxAtOriginal(svg);

        });
        window.addEventListener('mouseup', function () {
            if (isPanning) {
                isPanning = false;
                svg.style.cursor = '';
            }
        });

        // Pinch Zoom
        let lastTouchDist = null;
        let lastMidpoint = null

        svg.addEventListener('touchstart', function (e) {
            if (e.touches.length === 2) {
                lastTouchDist = getTouchDist(e.touches[0], e.touches[1]);
                lastMidpoint = getTouchMidpoint(e.touches[0], e.touches[1]);
            }
        }, { passive: false });

        svg.addEventListener('touchmove', function (e) {
            if (e.touches.length === 2 && lastTouchDist !== null) {
                e.preventDefault();
                let [x, y, w, h] = svg.getAttribute('viewBox').split(' ').map(Number);

                const newDist = getTouchDist(e.touches[0], e.touches[1]);
                const scale = lastTouchDist / newDist;

                let newW = w * scale;
                let newH = h * scale;

                // Clamp zoom
                const minW = fullViewBox.w * 0.1, maxW = fullViewBox.w;
                if (newW < minW) { newW = minW; newH = minW * h / w; }
                if (newW > maxW) { newW = maxW; newH = maxW * h / w; }

                // Center zoom on midpoint
                const svgPoint = svg.createSVGPoint();
                svgPoint.x = lastMidpoint.x;
                svgPoint.y = lastMidpoint.y;
                const svgP = svgPoint.matrixTransform(svg.getScreenCTM().inverse());

                let newX = svgP.x - ((svgP.x - x) * (newW / w));
                let newY = svgP.y - ((svgP.y - y) * (newH / h));

                // Clamp pan to bounds
                newX = Math.max(fullViewBox.x, Math.min(newX, fullViewBox.x + fullViewBox.w - newW));
                newY = Math.max(fullViewBox.y, Math.min(newY, fullViewBox.y + fullViewBox.h - newH));

                svg.setAttribute('viewBox', `${newX} ${newY} ${newW} ${newH}`);

                lastTouchDist = newDist;
                lastMidpoint = getTouchMidpoint(e.touches[0], e.touches[1]);

                // Enable/disable zoom out button
                const zoomOutBtn = document.getElementById('zoom-out-btn');
                if (zoomOutBtn) zoomOutBtn.disabled = isViewBoxAtOriginal(svg);
            }
        }, { passive: false });

        svg.addEventListener('touchend', function (e) {
            if (e.touches.length < 2) {
                lastTouchDist = null;
                lastMidpoint = null;
            }
        }, { passive: false });

        // Helper functions for pinch zoom
        function getTouchDist(touch1, touch2) {
            const dx = touch2.clientX - touch1.clientX;
            const dy = touch2.clientY - touch1.clientY;
            return Math.sqrt(dx * dx + dy * dy);
        }
        function getTouchMidpoint(touch1, touch2) {
            return {
                x: (touch1.clientX + touch2.clientX) / 2,
                y: (touch1.clientY + touch2.clientY) / 2
            };
        }

        // --- Touch Drag/Pan for Single Finger ---
        let isTouchPanning = false;
        let touchPanStart = null;
        let touchPanViewBox = null;

        svg.addEventListener('touchstart', function (e) {
            if (e.touches.length === 1) {
                isTouchPanning = true;
                touchPanStart = { x: e.touches[0].clientX, y: e.touches[0].clientY };
                touchPanViewBox = svg.getAttribute('viewBox').split(' ').map(Number);
                svg.style.cursor = 'grab';
            }
        }, { passive: false });

        svg.addEventListener('touchmove', function (e) {
            if (isTouchPanning && e.touches.length === 1 && touchPanStart && touchPanViewBox) {
                e.preventDefault();
                let dx = (e.touches[0].clientX - touchPanStart.x) * (touchPanViewBox[2] / svg.clientWidth);
                let dy = (e.touches[0].clientY - touchPanStart.y) * (touchPanViewBox[3] / svg.clientHeight);
                let newX = touchPanViewBox[0] - dx;
                let newY = touchPanViewBox[1] - dy;

                // Clamp pan to bounds
                newX = Math.max(fullViewBox.x, Math.min(newX, fullViewBox.x + fullViewBox.w - touchPanViewBox[2]));
                newY = Math.max(fullViewBox.y, Math.min(newY, fullViewBox.y + fullViewBox.h - touchPanViewBox[3]));

                svg.setAttribute('viewBox', `${newX} ${newY} ${touchPanViewBox[2]} ${touchPanViewBox[3]}`);

                // Enable/disable zoom out button
                const zoomOutBtn = document.getElementById('zoom-out-btn');
                if (zoomOutBtn) zoomOutBtn.disabled = isViewBoxAtOriginal(svg);
            }
        }, { passive: false });

        svg.addEventListener('touchend', function (e) {
            if (isTouchPanning && e.touches.length === 0) {
                isTouchPanning = false;
                touchPanStart = null;
                touchPanViewBox = null;
                svg.style.cursor = '';
            }
        }, { passive: false });
    }

    // Attach to modal show event (Bootstrap 5)
    document.getElementById('siteMapModal').addEventListener('shown.bs.modal', function () {
        setTimeout(setupEnhancedNavigation, 350); // Wait for SVG to load
    });
})();

function updateMiniMap() {
    const miniMapClonedSvg = window.miniMapClonedSvg;
    const mainSvg = document.querySelector('.svg-map svg');

    if (!miniMapClonedSvg || !mainSvg) return;

    // Get all site elements from both SVGs
    const mainSites = mainSvg.querySelectorAll('[id]');
    const miniSites = miniMapClonedSvg.querySelectorAll('[id]');

    // Create a map of main site states
    const siteStates = new Map();
    mainSites.forEach(site => {
        if (site.id) {
            const classList = Array.from(site.classList);
            siteStates.set(site.id, {
                available: classList.includes('site-available'),
                unavailable: classList.includes('site-unavailable'),
                selected: classList.includes('site-selected')
            });
        }
    });

    // Apply states to mini map sites with styling
    miniSites.forEach(miniSite => {
        if (miniSite.id && siteStates.has(miniSite.id)) {
            const state = siteStates.get(miniSite.id);

            // Clear existing classes and styles
            miniSite.classList.remove('site-available', 'site-unavailable', 'site-selected');
            miniSite.style.fill = '';
            miniSite.style.stroke = '';

            // Apply current state with explicit colors
            if (state.selected) {
                miniSite.classList.add('site-selected');
                miniSite.style.fill = 'var(--gold-accent, #ffd700)';
                miniSite.style.stroke = '#b8860b';
            } else if (state.available) {
                miniSite.classList.add('site-available');
                miniSite.style.fill = 'var(--accent-color, #28a745)';
                miniSite.style.stroke = '#1e7e34';
            } else if (state.unavailable) {
                miniSite.classList.add('site-unavailable');
                miniSite.style.fill = 'var(--secondary-color, #6c757d)';
                miniSite.style.stroke = '#545b62';
            }
        } else {
            // Default styling for sites not in the state map
            miniSite.style.fill = '#e9ecef';
            miniSite.style.stroke = '#adb5bd';
        }
    });

    console.log('Mini map updated with current site states and styling');
}

function setupMiniMap(svg, fullViewBox) {
    const miniMapContainer = document.getElementById('mini-map-container');
    const miniMapSvg = document.getElementById('mini-map');
    if (!miniMapContainer || !miniMapSvg || !svg) return;

    // Clone the main SVG
    const clonedSvg = svg.cloneNode(true);
    clonedSvg.removeAttribute('id');

    // Remove all event listeners and interactive elements from clone
    clonedSvg.querySelectorAll('*').forEach(el => {
        el.removeAttribute('onclick');
        el.style.pointerEvents = 'none';

        // Reset any existing fills to ensure proper styling
        if (el.tagName !== 'text') {
            el.style.fill = '';
            el.style.stroke = '';
        }
    });

    // Clear and add the cloned SVG
    miniMapSvg.innerHTML = '';
    miniMapSvg.appendChild(clonedSvg);

    // Create viewport rectangle overlay
    const viewportRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    viewportRect.classList.add('minimap-viewport');
    viewportRect.style.fill = 'none';
    viewportRect.style.stroke = '#007bff';
    viewportRect.style.strokeWidth = '2';
    viewportRect.style.strokeDasharray = '5,5';
    miniMapSvg.appendChild(viewportRect);

    // Set viewBox to match full map
    miniMapSvg.setAttribute('viewBox', `${fullViewBox.x} ${fullViewBox.y} ${fullViewBox.w} ${fullViewBox.h}`);

    // Show minimap
    miniMapContainer.style.display = 'block';

    // Store reference to cloned SVG for updates
    window.miniMapClonedSvg = clonedSvg;

    // Update the viewport rectangle to show current view
    function updateViewportRect() {
        const currentViewBox = svg.getAttribute('viewBox');
        const [vx, vy, vw, vh] = currentViewBox.split(' ').map(Number);

        const isAtOriginal = currentViewBox === `${fullViewBox.x} ${fullViewBox.y} ${fullViewBox.w} ${fullViewBox.h}`;

        if (isAtOriginal) {
            viewportRect.style.display = 'none'; //  Hide viewport when at original zoom
        } else {
            viewportRect.style.display = 'block'; //Show it otherwise
            viewportRect.setAttribute('x', vx);
            viewportRect.setAttribute('y', vy);
            viewportRect.setAttribute('width', vw);
            viewportRect.setAttribute('height', vh);

            // Hide when fully zoomed out
            if (svg.getAttribute('viewBox') === `${fullViewBox.x} ${fullViewBox.y} ${fullViewBox.w} ${fullViewBox.h}`) {
                viewportRect.style.opacity = '0';
            } else {
                viewportRect.style.opacity = '1';
            }
        }
    }

    // Initial viewport update
    updateViewportRect();

    // Set up mutation observer to watch for viewBox changes
    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            if (mutation.type === 'attributes' && mutation.attributeName === 'viewBox') {
                updateViewportRect();
            }
        });
    });

    observer.observe(svg, {
        attributes: true,
        attributeFilter: ['viewBox']
    });

    // Store observer for cleanup
    window.miniMapObserver = observer;

    // Initial styling update
    setTimeout(() => {
        updateMiniMap();
    }, 100);
}

// Expose deselectSite for external use
window.deselectSite = deselectSite;

// Initialize on page load
document.addEventListener('DOMContentLoaded', loadSvgMap);