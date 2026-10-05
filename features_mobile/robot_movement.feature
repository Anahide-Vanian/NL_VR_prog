Feature: Robot movement
  Move the user-selected C1-2-box from C1 to the selected target slot at B3-1.

  Scenario: Move selected C1-2-box to B3-1
    Given the property position of the robot "Bob" is C2
    When the robot "Bob" moves to position C1
    And the robot "Bob" picks the box "C1-2-box" from the slot "2"
    And the robot "Bob" moves to position B3
    And the robot "Bob" releases the box "C1-2-box" to the slot "1"
    Then the property position of the robot "Bob" is B3

